using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class BoxFearAttack : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private Buff _thisBuff;
        private float _manaTimer = 0f;
        private Minion _boxMinion;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _thisBuff = buff;
            _boxMinion = unit as Minion;

            if (_boxMinion == null) return;

            _boxMinion.Stats.ManaPoints.BaseValue = 5.0f;
            _boxMinion.Stats.CurrentMana = 5f;
            _boxMinion.SetStatus(StatusFlags.Invulnerable, false);


            if (!_boxMinion.IsDead)
            {
                CheckForTargets(_boxMinion);
            }

            var units = EnumerateValidUnitsInRange(_boxMinion, _boxMinion.Position, 500f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral);

            var variables = new BuffVariables();
            variables.Set("SlowPercent", 0.40f);
            foreach (var u in units)
            {
                if (u is not LaneTurret)
                {
                    AddBuff("Fear", 3f, 1, ownerSpell, u, _boxMinion, buffVariables: variables);
                }
            }
        }

        public void OnUpdate(float diff)
        {
            if (_boxMinion == null || _boxMinion.IsDead)
            {
                return;
            }

            if (!_boxMinion.IsAttacking)
            {
                CheckForTargets(_boxMinion);
            }

            if (_boxMinion.TargetUnit != null)
            {
                float distSq = Vector2.DistanceSquared(_boxMinion.Position, _boxMinion.TargetUnit.Position);

                float actualRange = _boxMinion.Stats.Range.Total + _boxMinion.TargetUnit.CollisionRadius + _boxMinion.CollisionRadius;
                float rangeSq = actualRange * actualRange;

                if (distSq > rangeSq)
                {
                    _boxMinion.SetTargetUnit(null, true);
                }
            }

            _manaTimer += diff;
            if (_manaTimer >= 1000f)
            {
                _manaTimer = 0f;
                _boxMinion.Stats.CurrentMana -= 1;
                if (_boxMinion.Stats.CurrentMana <= 0)
                {
                    _boxMinion.Die(CreateDeathData(false, 0, _boxMinion, _boxMinion, DamageType.DAMAGE_TYPE_TRUE,
                        DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
                }
            }
        }

        public void CheckForTargets(Minion boxMinion)
        {
            if (boxMinion == null) return;

            float searchRange = boxMinion.Stats.Range.Total;

            var units = GetUnitsInRange(boxMinion, boxMinion.Position, searchRange, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions);

            AttackableUnit nextTarget = null;
            var nextTargetPriority = ClassifyUnit.DEFAULT;

            foreach (var u in units)
            {
                if (u.Status.HasFlag(StatusFlags.Stealthed) || u == boxMinion)
                {
                    continue;
                }

                if (boxMinion.TargetUnit == null)
                {
                    var priority = boxMinion.ClassifyTarget(u);

                    if (nextTarget == null || priority < nextTargetPriority)
                    {
                        nextTarget = u;
                        nextTargetPriority = priority;
                    }
                }
                else
                {
                    if (boxMinion.TargetUnit is Champion)
                    {
                        continue;
                    }

                    if (!(u is Champion)) continue;

                    nextTarget = u;
                    break;
                }
            }

            if (nextTarget != null && boxMinion.TargetUnit != nextTarget)
            {
                boxMinion.SetTargetUnit(nextTarget, true);
                //boxMinion.UpdateMoveOrder(OrderType.AttackTo, true);
            }
        }
    }
}