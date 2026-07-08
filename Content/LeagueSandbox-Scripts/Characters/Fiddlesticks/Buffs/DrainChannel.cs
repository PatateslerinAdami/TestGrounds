using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

// 4.20 target-side debuff for Fiddle W. REPLAY-VERIFIED (096729f… ARAM dump):
//   AddBuff("DrainChannel", 5.0f, 1, spell, <target>, FiddleSticks)  /* DAMAGE */
// Named "DrainChannel" in 4.20 — NOT "Drain" as the S1 DrainChannel.lua's explicit BuffName="Drain"
// (S1→4.20 rename; raw replay wins over the S1 lua). Pure marker; the per-tick damage lives in the
// DrainChannel spell script (Fiddlesticks/W.cs), not here.
public class DrainChannel : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType    = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}
