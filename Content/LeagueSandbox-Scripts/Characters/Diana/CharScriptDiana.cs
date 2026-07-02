using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptDiana : ICharScript {
    private ObjAIBase _diana;
    private Spell _spell;
    private int _counter;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _diana = owner;
        _spell = spell;
        ApiEventManager.OnHitUnit.AddListener(this, _diana, OnHit);
    }

    public void OnPostActivate(ObjAIBase owner, Spell spell = null)
    {
        AddBuff("DianaCombatBuff", 250000f, 1, _spell, _diana, _diana, true);
    }

    private void OnHit(DamageData data)
    {
        AddBuff("DianaPassive", 4f, 1, _spell, _diana, _diana);
        if (_diana.GetBuffsWithName("DianaPassive").Count == 3)
        {
            AddParticleTarget(_diana, _diana, "Diana_Base_P.troy", data.Target);
            var ap = _diana.Stats.AbilityPower.Total * 0.8f;
            var dmg = 20f + 5 * (_diana.Stats.Level - 1) + ap;
            foreach (var unit in GetUnitsInCone(_diana, _diana.Position, data.Target.Position - _diana.Position, 175f, 180f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes))
            {
                unit.TakeDamage(_diana, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
            }

            var buffs = _diana.GetBuffsWithName("DianaPassive");
            foreach (var buff in buffs)
            {
                RemoveBuff(buff);
            }
        }
    }
}