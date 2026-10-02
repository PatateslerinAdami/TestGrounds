using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class OdynsVeil : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _unit;
        private Buff _buff;
        private float _storedDamage;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            _buff = buff;
            buff.SetToolTipVar(0, _storedDamage);
            ApiEventManager.OnPreTakeDamage.AddListener(this, unit, OnPreTakeDamage);
        }

        private void OnPreTakeDamage(DamageData data)
        {
            if (_unit.IsDead || data.DamageType != DamageType.DAMAGE_TYPE_MAGICAL
                || !_unit.CanTakeDamage(data.DamageType)
                || data.PostMitigationDamage <= 0.0f)
            {
                return;
            }

            var reduced = data.PostMitigationDamage * 0.1f;
            data.PostMitigationDamage -= reduced;
            _storedDamage = Math.Min(200.0f, _storedDamage + reduced);
            _buff.SetToolTipVar(0, _storedDamage);
        }

        public float Discharge()
        {
            var damage = 200.0f + _storedDamage;
            _storedDamage = 0.0f;
            _buff.SetToolTipVar(0, _storedDamage);
            return damage;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
