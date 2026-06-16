using System.Linq;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Players;
using GameServerCore.Enums;
using static GameServerCore.Packets.Enums.Channel;

namespace LeagueSandbox.GameServer.Chatbox.Commands;

/// <summary>
/// !test <spellSlot> — Level up to 6, learn all spells, cast the given spell slot,
/// and log all resulting packets to the test client.
/// 
/// Usage:  !test Q   or   !test 0   (0=Q,1=W,2=E,3=R)
/// </summary>
public class TestSpellCommand : ChatCommandBase
{
    private readonly PlayerManager _playerManager;

    public override string Command => "testspell";
    public override string Syntax => $"{Command} <Q|W|E|R|0-3>";

    public TestSpellCommand(ChatCommandManager chatCommandManager, Game game)
        : base(chatCommandManager, game)
    {
        _playerManager = game.PlayerManager;
    }

    public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
    {
        var split = arguments.ToLower().Split(' ');
        if (split.Length < 1)
        {
            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR);
            ShowSyntax();
            return;
        }

        var peerInfo = _playerManager.GetPeerInfo(userId);
        var champion = peerInfo?.Champion;
        if (champion == null)
        {
            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.ERROR, "No champion found.");
            return;
        }

        // Parse slot: Q=0, W=1, E=2, R=3, or numeric 0-3
        byte slot = 99;
        var arg = split[0];
        switch (arg)
        {
            case "q": slot = 0; break;
            case "w": slot = 1; break;
            case "e": slot = 2; break;
            case "r": slot = 3; break;
            default:
                if (!byte.TryParse(arg, out slot) || slot > 3)
                {
                    ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.ERROR, "Slot must be Q/W/E/R or 0-3");
                    return;
                }
                break;
        }

        int targetLevel = champion.Stats.Level < 6 ? 6 : champion.Stats.Level;

        ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO,
            $"[TEST] Champion={champion.Model} Level={champion.Stats.Level} → {targetLevel}");

        // Step 1: Level champion to target level
        while (champion.Stats.Level < targetLevel)
        {
            champion.LevelUp(true);
        }

        // Step 2: Level all spells (QWER)
        var spellNames = new[] { champion.Spells[0], champion.Spells[1], champion.Spells[2], champion.Spells[3] };
        for (byte s = 0; s < 4; s++)
        {
            var sp = spellNames[s];
            while (sp != null && sp.CastInfo.SpellLevel < 1 && champion.SkillPoints > 0)
            {
                champion.LevelUpSpell(s);
            }
        }

        // Step 3: Cast the spell
        ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO,
            $"[TEST] Casting slot={slot}");

        champion.SetCastSpell(spellNames[slot]);

        ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO,
            $"[TEST] Done. Check packets for slot={slot}");
    }
}
