using Buffs;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class RivenTriCleave : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            TriggersSpellCasts = true
        };

        private ObjAIBase _owner;
        private AttackableUnit _target;
        private int _currentQStage = 0;
        Spell _spell;
        public void OnActivate(ObjAIBase owner, Spell spell) 
        {
            _spell = spell;
            _owner = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _owner = owner;
            _target = GetClosestTeamUnitInRange(end, 200, true, CustomConvert.GetEnemyTeam(_owner.Team));
        }

        public void OnSpellPostCast(Spell spell)
        {

            AddBuff("RivenTriCleave", 4.0f, 1, spell, _owner, _owner);
            AddBuff("RivenPassiveAABoost", 5f, 1, spell, _owner, _owner, false);

            var buff = _owner.GetBuffWithName("RivenTriCleave");
            _currentQStage = buff != null ? buff.StackCount : 1;

            ApiEventManager.OnMoveEnd.AddListener(this, _owner, OnDashFinished, true);

            float q1q2Range = 225f;
            float q3Range = 250f;
            float dashSpeedBase = 700f;

            string animName = "";
            string trailParticle = "";
            float range = q1q2Range;

            switch (_currentQStage)
            {
                case 1:
                    animName = "Spell1A";
                    trailParticle = "Riven_Base_Q_01_Wpn_Trail.troy";
                    range = q1q2Range;
                    break;
                case 2:
                    animName = "Spell1B";
                    trailParticle = "Riven_Base_Q_02_Wpn_Trail.troy";
                    range = q1q2Range;
                    break;
                case 3:
                    animName = "Spell1C";
                    trailParticle = "Riven_Base_Q_03_Wpn_Trail.troy";
                    range = q3Range;
                    buff?.DeactivateBuff();
                    break;
            }

            PlayAnimation(_owner, animName, 0.75f);

            var dashPos = GetDashDestination(range);

            var distance = (dashPos - _owner.Position).Length();
            var newSpeed = (dashSpeedBase / range) * distance;
            float verticalSpeed = (_currentQStage == 3) ? 50 : 0;

            ForceMovement(_owner, animName, dashPos, newSpeed, 0, verticalSpeed, 0, false);

            if (!string.IsNullOrEmpty(trailParticle))
            {
                AddParticle(_owner, _owner, trailParticle, _owner.Position, size: (_currentQStage == 3 ? -1 : 1), bone: "chest");
            }
        }

        public void OnDashFinished(AttackableUnit unit)
        {
            if (!(unit is ObjAIBase owner)) return;

            _owner.SkipNextAutoAttack();

            float radius = (_currentQStage == 3) ? 300f : 260f;
            string detonateParticle = $"exile_Q_0{_currentQStage}_detonate.troy";
            string hitParticle = $"exile_Q_tar_0{_currentQStage}.troy";

            AddParticle(_owner, null, detonateParticle, GetPointFromUnit(_owner, 125f));
            ApplyQDamage(_owner, radius, hitParticle);
        }

        private void ApplyQDamage(ObjAIBase owner, float radius, string specificHitParticle)
        {
            var damage = 10;

            var damageCenter = GetPointFromUnit(_owner, 80f);
            var units = GetUnitsInRange(damageCenter, radius, true);

            foreach (var target in units)
            {
                if (IsValidTarget(target))
                {
                    target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, _spell);
                    if (_currentQStage == 3)
                    {
                        AddBuff("Pulverize", 0.75f, 1, _spell, target, _owner);
                    }

                    AddParticleTarget(_owner, target, "RivenQ_tar.troy", target, 10f);
                    AddParticleTarget(_owner, target, specificHitParticle, target, 10f);
                    AddParticleTarget(_owner, target, "exile_Q_tar_04.troy", target, 10f);
                }
            }
        }

        private bool IsValidTarget(AttackableUnit target)
        {
            return target.Team != _owner.Team
                   && !(target is ObjBuilding || target is BaseTurret)
                   && target.Status.HasFlag(StatusFlags.Targetable)
                   && !target.IsDead;
        }

        private Vector2 GetDashDestination(float maxDashRange)
        {
            var destination = GetPointFromUnit(_owner, maxDashRange);

            if (_target != null)
            {
                FaceDirection(_target.Position, _owner);
                var direction = Vector2.Normalize(_target.Position - _owner.Position);

                float distToTarget = Vector2.Distance(_owner.Position, _target.Position);

                if (distToTarget < maxDashRange)
                {
                    float halfDistance = distToTarget / 2.0f;
                    if (halfDistance < 30f) halfDistance = 30f;
                    destination = _owner.Position + (direction * halfDistance);
                }
                else
                {
                    destination = _owner.Position + (direction * maxDashRange);
                }
            }

            return destination;
        }
    }
}