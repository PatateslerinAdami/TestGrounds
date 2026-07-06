using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class SionPassiveSoundEnd : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.INTERNAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        PersistsThroughDeath = true,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}