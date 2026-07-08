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

internal class SionPassiveDelay : IBuffGameScript
{
    private ObjAIBase _sion;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        PersistsThroughDeath = true,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _sion = buff.SourceUnit;
        PlayAnimation(unit, "SionReanimate");
        HideHealthBar(unit, hide: true);
        unit.SetStatus(StatusFlags.Targetable, false);
        unit.SetStatus(StatusFlags.Invulnerable, true);
        unit.SetStatus(StatusFlags.CanMove, false);
        unit.SetStatus(StatusFlags.CanAttack, false);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        unit.SetStatus(StatusFlags.Targetable, true);
        unit.SetStatus(StatusFlags.Invulnerable, false);
        unit.SetStatus(StatusFlags.CanMove, true);
        unit.SetStatus(StatusFlags.CanAttack, true);
        HideHealthBar(unit, hide: false);
        AddBuff("SionPassiveZombie", 8f, 1, ownerSpell, _sion, _sion);
    }
}
