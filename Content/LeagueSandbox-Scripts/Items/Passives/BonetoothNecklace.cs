using System;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3450 : UniqueStatItem
    {
        private ObjAIBase _owner;
        private Buff _state;
        private bool _brush;
        private float _combat;
        private Buff _lastUltimate;
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            _state = owner.GetBuffWithName("BonetoothNecklace") ?? AddBuff(
                "BonetoothNecklace",
                0,
                1,
                null,
                owner,
                owner,
                true
            );
            ApiEventManager.OnKill.AddListener(this, owner, Trophy);
            ApiEventManager.OnAssist.AddListener(this, owner, (unit, data) => Trophy(data));
            ApiEventManager.OnTakeDamage.AddListener(this, owner, Combat);
            ApiEventManager.OnDealDamage.AddListener(this, owner, Combat);
            ApiEventManager.OnEnterGrass.AddListener(this, owner, unit => _brush = true);
            ApiEventManager.OnLeaveGrass.AddListener(this, owner, unit => _brush = false);
        }

        private void Trophy(DeathData data)
        {
            if (data.Unit is Champion)
            {
                _state.Variables.Set("trophies", _state.Variables.GetInt("trophies") + 1);
                _state.SetToolTipVar(0, _state.Variables.GetInt("trophies"));
            }
        }

        private void Combat(DamageData data)
        {
            if (data.PostMitigationDamage > 0)
            {
                _combat = 5000;
            }
        }

        public override void OnUpdate(float diff)
        {
            _combat = Math.Max(0, _combat - diff);
            var trophies = _state.Variables.GetInt("trophies");
            var ultimate = _owner.GetBuffWithName("RengarRBuff");
            if (ultimate != null && ultimate != _lastUltimate && trophies >= 12)
            {
                ultimate.ExtendDuration(5);
                _lastUltimate = ultimate;
            }

            _owner.RemoveStatModifier(StatsModifier);
            StatsModifier.MoveSpeed.FlatBonus = trophies >= 3 && (_combat == 0 || _brush) ? 25 : 0;
            StatsModifier.Range.FlatBonus = trophies >= 6 && _owner.HasBuff("RengarPassiveBuff") ? 125 : 0;
            StatsModifier.MoveSpeed.PercentBonus = trophies >= 20 && ultimate != null
                && _owner.Status.HasFlag(StatusFlags.Stealthed) ? Math.Max(0, ultimate.GetStatsModifier().MoveSpeed.PercentBonus) : 0;
            _owner.AddStatModifier(StatsModifier);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
