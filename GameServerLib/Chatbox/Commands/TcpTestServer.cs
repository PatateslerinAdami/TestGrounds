using GameServerCore.Enums;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Chatbox.Commands;

/// <summary>
/// TCP test server — listens on port 5190 and accepts commands like:
///   "test Riven R" → levels to 6, casts R, returns result
///   "info" → champion status
/// </summary>
public class TcpTestServer
{
    private readonly Game _game;
    private TcpListener? _listener;
    private Thread? _thread;
    private volatile bool _running;

    public TcpTestServer(Game game) { _game = game; }

    public void Start()
    {
        _running = true;
        _thread = new Thread(Listen) { IsBackground = true, Name = "TcpTestServer" };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
    }

    private void Listen()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, 5190);
            _listener.Start();
            Console.WriteLine("[TestServer] TCP listening on 127.0.0.1:5190");

            while (_running)
            {
                if (!_listener.Pending())
                {
                    Thread.Sleep(100);
                    continue;
                }

                using var client = _listener.AcceptTcpClient();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var result = ProcessCommand(line.Trim());
                writer.WriteLine(result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TestServer] Error: {ex.Message}");
        }
    }

    private string ProcessCommand(string cmd)
    {
        var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "ERR: empty command";

        switch (parts[0].ToLower())
        {
            case "test":
                return HandleTest(parts);
            case "info":
                return HandleInfo();
            case "ping":
                return "PONG";
            default:
                return $"ERR: unknown command '{parts[0]}'. Use: test <champ> <Q|W|E|R>";
        }
    }

    private string HandleTest(string[] parts)
    {
        if (parts.Length < 2)
            return "ERR: usage: test <champ> <Q|W|E|R>";

        string champ = parts[1];
        string spell = parts.Length >= 3 ? parts[2].ToUpper() : "R";
        byte slot = spell switch { "Q" => 0, "W" => 1, "E" => 2, "R" => 3, _ => 3 };

        // Find champion — search by model OR by the champion name from player config
        var champion = _game.ObjectManager.GetAllChampions()
            .FirstOrDefault(c => c.Model.Equals(champ, StringComparison.OrdinalIgnoreCase)
                || _game.Config.Players.Any(p => p.Name == "Test" && (
                    p.Champion.Equals(champ, StringComparison.OrdinalIgnoreCase)
                    || c.Model.Contains(champ, StringComparison.OrdinalIgnoreCase))));

        if (champion == null)
            return $"ERR: champion '{champ}' not found";

        // Wait for game to actually be running (forcedStart completes)
        for (int i = 0; i < 80 && !_game.IsRunning; i++)
            Thread.Sleep(100);
        if (!_game.IsRunning)
            return "ERR: game not running yet";

        // Kill all turrets so the teleported enemy doesn't die to fountain
        foreach (var obj in _game.ObjectManager.GetObjects().Values)
        {
            if (obj is BaseTurret turret)
                turret.TakeDamage(turret, 99999f, DamageType.DAMAGE_TYPE_TRUE,
                    DamageSource.DAMAGE_SOURCE_RAW, DamageResultType.RESULT_NORMAL);
        }

        while (champion.Stats.Level < 6)
            champion.LevelUp(true);

        // Level spells
        var spellObjs = champion.Spells.Values.ToArray();
        for (byte s = 0; s < 4 && s < spellObjs.Length; s++)
        {
            var spellObj = spellObjs[s];
            while (spellObj != null && spellObj.CastInfo.SpellLevel < 1 && champion.SkillPoints > 0)
                champion.LevelUpSpell(s);
        }

        // Find nearest enemy champion and teleport them close
        var enemy = _game.ObjectManager.GetAllChampions()
            .Where(c => c.Team != champion.Team && !c.IsDead)
            .OrderBy(c => Vector2.Distance(c.Position, champion.Position))
            .FirstOrDefault();

        if (enemy != null)
        {
            // Teleport enemy right next to our champion for reliable hit testing
            var tpPos = new Vector2(champion.Position.X + 200, champion.Position.Y);
            enemy.SetPosition(tpPos);
            Thread.Sleep(50);
        }

        float enemyHpBefore = enemy?.Stats.CurrentHealth ?? 0;
        float enemyHpMax = enemy?.Stats.HealthPoints.Total ?? 0;

        // Count existing particles (rough — count objects near the champion)
        int particlesBefore = _game.ObjectManager.GetObjects().Count(o => o is Particle);

        // Cast at enemy position, targeting enemy
        var castSpell = champion.Spells[(short)slot];
        if (castSpell == null) return "ERR: spell not found at slot " + slot;

        var castPos = enemy?.Position ?? champion.Position;
        castSpell.SetCooldown(0, false);  // reset cooldown so spell is STATE_READY
        castSpell.Cast(castPos, castPos, enemy);

        // Wait for spell execution
        Thread.Sleep(200);

        float enemyHpAfter = enemy?.Stats.CurrentHealth ?? 0;
        int particlesAfter = _game.ObjectManager.GetObjects().Count(o => o is Particle);
        float dmg = enemyHpBefore - enemyHpAfter;
        int newParticles = particlesAfter - particlesBefore;

        var spellList = string.Join(",", champion.Spells.Values.Take(4).Select(s =>
            $"{s.SpellName}({s.CastInfo.SpellLevel})"));

        string dmgInfo = enemy != null
            ? $" dmg={dmg:F0}/{enemyHpMax:F0}HP enemy={enemy.Model}"
            : " dmg=N/A (no enemy)";

        string pfxInfo = newParticles > 0 ? $" particles=+{newParticles}" : " particles=0";

        return $"OK: {champ} model={champion.Model} lvl={champion.Stats.Level} " +
               $"cast={spell}({slot}) spells=[{spellList}]" +
               $"{dmgInfo}{pfxInfo}";
    }

    private string HandleInfo()
    {
        var champs = _game.ObjectManager.GetAllChampions();
        if (champs.Count == 0) return "OK: no champions";

        var lines = champs.Select(c =>
            $"{c.Model} lvl={c.Stats.Level} hp={c.Stats.CurrentHealth:F0}/{c.Stats.HealthPoints.Total:F0} " +
            $"pos=({c.Position.X:F0},{c.Position.Y:F0}) spells=[{string.Join(",", c.Spells.Values.Take(4).Select(s => s.CastInfo.SpellLevel))}]");

        return string.Join("\n", lines);
    }
}
