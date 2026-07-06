using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

// Visible Focus counter (0..100). Riot sends this as a plain AURA buff (replay-verified:
// NPC_BuffAdd2 BuffType=1) and drives the number via the Count field + ShowInTrackerUI, NOT
// as a COUNTER-type buff. Sending BuffType=COUNTER (26) makes the 4.20 client mishandle the
// buff (extra NPC_BuffUpdateNumCounter + wrong client-side structure) → !mStack.empty() →
// ACCESS_VIOLATION on the guaranteed-crit auto attack. Keep it AURA.
public class AsheCritChance : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 100,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}
