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

namespace Buffs
{
    public class AatroxPassiveDeath : IBuffGameScript
    {
        private ObjAIBase _owner;
        private float _blood, _heal, _elapsed;
        private int _steps;
        private Particle _effect;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType= BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            _blood = _owner.GetPAR() / 6;
            _heal = (_owner.GetPAR() + 0.35f * _owner.GetMaxPAR()) / 6;
            CancelDash(_owner);
            if (_owner.IsAttacking)
            {
                _owner.CancelAutoAttack(false, true);
            }

            _owner.StopMovement();
            _owner.SetTargetUnit(null, true);
            buff.SetStatusEffect(StatusFlags.Targetable, false);
            buff.SetStatusEffect(StatusFlags.Invulnerable, true);
            buff.SetStatusEffect(StatusFlags.CanMove, false);
            buff.SetStatusEffect(StatusFlags.CanAttack, false);
            buff.SetStatusEffect(StatusFlags.CanCast, false);
            _owner.Stats.CurrentHealth = 1;
            _effect = AddParticleTarget(_owner, _owner, Spells.AatroxHelpers.Effect(_owner, "Passive_Death_Activate"), _owner,
                buff.Duration);
            PlayAnimation(_owner, "Passive_Death", 3);
        }

        public void OnUpdate(float diff)
        {
            _elapsed += diff;
            int due = Math.Clamp((int)((_elapsed - 250 + 0.01f) / 500), 0, 6);
            while (_steps < due)
            {
                _owner.TakeHeal(_owner, _heal, HealType.SelfHeal);
                _owner.SpendPAR(_blood);
                _steps++;
            }

            if (_owner.GetBuffWithName("AatroxPassive") is { Remove: false, BuffScript: AatroxPassive passive })
            {
                passive.RefreshAttackSpeed();
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_effect);
            StopAnimation(_owner, "Passive_Death", fade: true);
            if (_owner.IsToRemove())
            {
                return;
            }

            AddParticleTarget(_owner, _owner, Spells.AatroxHelpers.Effect(_owner, "Passive_Death_End"), _owner, 0.5f);
            spell.SetCooldown(225, true);
            SetPARState(_owner, 1);
            AddBuff("AatroxPassiveActivate", 225, 1, spell, _owner, _owner);
        }
    }
}
