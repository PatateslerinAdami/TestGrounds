using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class TwoShivPoison : ISpellScript
    {
        private Spell _spell;
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target 
            },
            TriggersSpellCasts = true,
            CastingBreaksStealth = true,
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _spell = spell;
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnHit);
            if (owner is Champion) ApiEventManager.OnHitUnit.AddListener(this, owner, OnAttack);
        }

        private void OnAttack(GameServerLib.GameObjects.AttackableUnits.DamageData data)
        {
            ApplyPassive(_spell, _spell.CastInfo.Owner, data);
        }

        public static void ApplyPassive(Spell spell, ObjAIBase caster, GameServerLib.GameObjects.AttackableUnits.DamageData data)
        {
            int rank = spell.CastInfo.SpellLevel;
            if (rank == 0 || spell.CurrentCooldown > 0 || spell.State != SpellState.STATE_READY || data.Damage <= 0
                || data.DamageResultType == DamageResultType.RESULT_MISS
                || data.Target is not (Champion or Minion)) return;

            var variables = new BuffVariables();
            variables.Set("Slow", spell.SpellData.GetEffectAmount(2, rank) / 100);
            AddBuff("TwoShivPoison", spell.SpellData.GetEffectAmount(4, rank), 1, spell,
                data.Target, caster, buffVariables: variables);
            if (data.Target is Minion && data.Target is not Pet { IsClone: true })
            {
                variables = new BuffVariables();
                variables.Set("MissChance", spell.SpellData.GetEffectAmount(3, rank) / 100);
                AddBuff("TwoShivPoisonMiss", spell.SpellData.GetEffectAmount(4, rank), 1, spell,
                    data.Target, caster, buffVariables: variables);
            }
        }

        private void OnHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            missile?.SetToRemove();
            if (target == null || target.IsDead || !target.GetIsTargetableToTeam(spell.CastInfo.Owner.Team)) return;

            var owner = spell.CastInfo.Owner;
            int rank = spell.CastInfo.SpellLevel;
            float bonusAD = owner.Stats.AttackDamage.Total - owner.Stats.AttackDamage.BaseValue;
            float damage = spell.SpellData.GetEffectAmount(1, rank) + owner.Stats.AbilityPower.Total * spell.SpellData.Coefficient
                + bonusAD * spell.SpellData.Coefficient2;
            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
            if (!target.IsDead) ApplyPoison(target, spell.SpellData.GetEffectAmount(5, rank));
        }

        private void ApplyPoison(AttackableUnit target, float duration)
        {
            var variables = new BuffVariables();
            variables.Set("Slow", _spell.SpellData.GetEffectAmount(2, _spell.CastInfo.SpellLevel) / 100);
            AddBuff("TwoShivPoison", duration, 1, _spell, target, _spell.CastInfo.Owner, buffVariables: variables);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.RemoveListener(this);
            ApiEventManager.OnHitUnit.RemoveListener(this);
        }
    }
}
