using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class Deceive : IBuffGameScript
    {
        private Buff _buff;
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INVISIBILITY,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
            SealSpellSlot((ObjAIBase)unit, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, true);
            ownerSpell.SetCooldown(0, true);
            NotifyUnitStats(unit, reliable: true);
            unit.EnterStealth();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnDeath.RemoveListener(this);
            SealSpellSlot((ObjAIBase)unit, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, false);
            ownerSpell.SetCooldown(ownerSpell.GetCooldown(), true);
            NotifyUnitStats(unit, reliable: true);
            unit.ExitStealth();
        }
        private void OnDeath(DeathData data)
        {
            _buff.DeactivateBuff();
        }
    }

    public class DeceiveCritBonus : IBuffGameScript
    {
        private Buff _buff;
        private float _criticalMultiplier;
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            _criticalMultiplier = ownerSpell.SpellData.GetEffectAmount(1, ownerSpell.CastInfo.SpellLevel) / 100;
            StatsModifier.CriticalChance.FlatBonus = 1;
            unit.AddStatModifier(StatsModifier);
            ApiEventManager.OnHitUnit.AddListener(this, (ObjAIBase)unit, OnHit);
            ApiEventManager.OnMiss.AddListener(this, unit, OnMiss);
            ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
        }

        private void OnHit(DamageData data)
        {
            if (_buff.Remove || data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;

            float normalCrit = data.Attacker.Stats.CriticalDamage.Total;
            float empoweredCrit = _criticalMultiplier + normalCrit - 2;
            float multiplier = normalCrit > 0 ? empoweredCrit / normalCrit : 1;
            data.Damage *= multiplier;
            data.PostMitigationDamage *= multiplier;
            _buff.DeactivateBuff();
        }

        private void OnDeath(DeathData data)
        {
            _buff.DeactivateBuff();
        }

        private void OnMiss(AttackableUnit attacker, AttackableUnit target)
        {
            _buff.DeactivateBuff();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnHitUnit.RemoveListener(this);
            ApiEventManager.OnMiss.RemoveListener(this);
            ApiEventManager.OnDeath.RemoveListener(this);
        }
    }
}
