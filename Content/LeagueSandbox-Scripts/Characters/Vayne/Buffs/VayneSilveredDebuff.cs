using System;
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

internal class VayneSilveredDebuff : IBuffGameScript {
    private ObjAIBase _vayne;
    private Particle  _p;
    
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.DAMAGE,
        BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
        MaxStacks   = 100
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _vayne = ownerSpell.CastInfo.Owner;
        var spellLevel    = Math.Clamp((int)ownerSpell.CastInfo.SpellLevel, 1, 5);
        var percentDamage = 0.04f + 0.01f * (spellLevel - 1);
        var flatDamage    = 20f + 10f * (spellLevel - 1);

        var damage = flatDamage + unit.Stats.HealthPoints.Total * percentDamage;
        if (unit is Monster) {
            damage = Math.Min(damage, 200f);
        }

        unit.TakeDamage(_vayne, damage, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_PROC,
                        DamageResultType.RESULT_NORMAL);
        buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p);
    }
}
