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
    internal class NunuQBuffLizard : IBuffGameScript
    {

        private ObjAIBase _nunu;
        private Spell _spell;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };
        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _nunu = buff.SourceUnit;
            _spell = ownerSpell;
            // Wiki: basic attacks AND abilities deal bonus magic damage. OnDealDamage covers both
            // (DAMAGE_SOURCE_ATTACK + DAMAGE_SOURCE_SPELL); OnHitUnit alone would miss ability damage.
            ApiEventManager.OnDealDamage.AddListener(this, _nunu, OnDealDamage);
        }

        private void OnDealDamage(DamageData data)
        {
            // Only basic attacks + abilities proc it. Excluding this proc's own PROC-sourced damage
            // (and all other reactive/periodic sources) prevents infinite recursion.
            if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK
                && data.DamageSource != DamageSource.DAMAGE_SOURCE_SPELL)
            {
                return;
            }
            var dmg = _nunu.Stats.HealthPoints.Total * _spell.SpellData.EffectLevelAmount[4][_spell.CastInfo.SpellLevel];
            data.Target.TakeDamage(_nunu, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, DamageResultType.RESULT_NORMAL);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnDealDamage.RemoveListener(this, _nunu, OnDealDamage);
        }
    }
}