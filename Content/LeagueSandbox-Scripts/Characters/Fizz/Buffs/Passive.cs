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
    public class FizzPassive : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            SendHiddenToClient = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            buff.SetStatusEffect(StatusFlags.Ghosted, true);
            ApiEventManager.OnPreTakeDamage.AddListener(this, unit, Reduce);
            SyncTooltip();
        }

        private void Reduce(DamageData data)
        {
            if (data.IsAutoAttack && data.DamageType == DamageType.DAMAGE_TYPE_PHYSICAL)
            {
                data.PostMitigationDamage = MathF.Max(0, data.PostMitigationDamage - Reduction());
            }
        }

        private float Reduction()
        {
            return 4 + 2 * Math.Clamp((_buff.TargetUnit.Stats.Level - 1) / 3, 0, 5);
        }

        private void SyncTooltip()
        {
            _buff.SetToolTipVar(0, Reduction());
        }

        public void OnUpdate(float diff)
        {
            SyncTooltip();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FizzPassiveDisplayIcon : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            SendHiddenToClient = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }
}
