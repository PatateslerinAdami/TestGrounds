using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

// Applied to the unit hooked by Dredge Line for the pull + landing recovery.
// Replay (34a3cc3c): BuffAdd right after the hit list, removed ~0.84-1.05s later.
// The actual displacement CC comes from the ForceMovement in the Q script; this buff
// covers the stand-up window on top of it.
internal class NautilusAnchorDragRoot : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.STUN,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}
