using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class Drain : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new()
        {
            TriggersSpellCasts = true
        };
        public void OnSpellCast(Spell spell)
        {
            var target = spell.CastInfo.Targets.FirstOrDefault()?.Unit;
            if (target != null)
            {
                SpellCast(spell.CastInfo.Owner, 0, SpellSlotType.ExtraSlots, false, target, target.Position);
            }
        }
    }

    public class DrainChannel : ISpellScript
    {
        private ObjAIBase _owner;
        private AttackableUnit _target;
        private Spell _spell;
        private Spell _parentSpell; 

        private Particle _tetherParticle;
        private Particle _targetParticle;

        private float _tickTimer;
        private const float TICK_INTERVAL = 250f; 
        private const float LEASH_RANGE_SQR = 650f * 650f;

        public SpellScriptMetadata ScriptMetadata => new()
        {
            CastingBreaksStealth = true,
            IsDamagingSpell = true,
            ChannelDuration = 5.0f,
            TriggersSpellCasts = true
        };
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _owner = owner;
            _target = target;
            _spell = spell;
            _tickTimer = 0f; 
            _parentSpell = owner.GetSpell("Drain");
        }

        public void OnSpellChannel(Spell spell)
        {
            if (_owner == null || _target == null) return;

            AddBuff("DrainChannel", 5.0f, 1, spell, _owner, _owner);

            _tetherParticle = AddParticleTarget(_owner, _owner, "Drain", _target, 5.0f, bone: "spine", targetBone: "spine");
            _targetParticle = AddParticleTarget(_owner, _owner, "Fearmonger_cas.troy", _target, 5.0f);
        }

        public void OnUpdate(float diff)
        {
            if (_spell == null || _owner.ChannelSpell != _spell) return;

            if (_target == null || _target.IsDead || _owner.IsDead)
            {
                _spell.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
                return;
            }

            if (Vector2.DistanceSquared(_owner.Position, _target.Position) > LEASH_RANGE_SQR)
            {
                _spell.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
                return;
            }

            _tickTimer -= diff;

            if (_tickTimer <= 0)
            {
                PerformTick();
                _tickTimer += TICK_INTERVAL;
            }
        }

        private void PerformTick()
        {
            if (_parentSpell == null) return;

            var level = _parentSpell.CastInfo.SpellLevel;

            float damagePerSecond = 60f + (30f * (level - 1)) + (_owner.Stats.AbilityPower.Total * 0.45f);

            float damagePerTick = damagePerSecond * 0.25f;

            //Come back to check if we should heal by the damage or post mitigation damage.
            float healPercent = 0.60f + (0.05f * (level - 1));
            float healAmount = damagePerTick * healPercent;

            _target.TakeDamage(_owner, damagePerTick, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
            _owner.TakeHeal(_owner, healAmount);
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            Cleanup();
        }

        public void OnSpellPostChannel(Spell spell)
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (_owner != null)
            {
                RemoveBuff(_owner, "DrainChannel");
            }

            if (_tetherParticle != null)
            {
                RemoveParticle(_tetherParticle);
                _tetherParticle = null;
            }

            if (_targetParticle != null)
            {
                RemoveParticle(_targetParticle);
                _targetParticle = null;
            }
        }
    }
}