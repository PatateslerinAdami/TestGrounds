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

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _thisBuff = buff;

            var boxMinion = unit as Minion;
            if (boxMinion == null) return;
            boxMinion.Stats.ManaPoints.BaseValue = 5.0f;
            boxMinion.Stats.CurrentMana = 5f;
            boxMinion.SetStatus(StatusFlags.Invulnerable, false);

            if (!boxMinion.IsDead)
            {
                CheckForTargets(boxMinion);
            }

            var units = GetUnitsInRangeDiffTeam(boxMinion.Position, 500f, true, boxMinion);
            foreach (var u in units)
            {
                AddBuff("Fear", 3f, 1, ownerSpell, u, boxMinion);
            }
        }
        public void OnUpdate(float diff)
        {
            var boxMinion = _thisBuff.TargetUnit as Minion;
            if (boxMinion == null || boxMinion.IsDead)
            {
                return;
            }
            if (!boxMinion.IsAttacking)
            {
                CheckForTargets(boxMinion);
            }
            if (boxMinion.TargetUnit != null && Vector2.DistanceSquared(boxMinion.Position, boxMinion.TargetUnit.Position) > (boxMinion.Stats.Range.Total * boxMinion.Stats.Range.Total))
            {
                boxMinion.SetTargetUnit(null, true);
            }
            _manaTimer += diff;
            if (_manaTimer >= 1000f)
            {
                _manaTimer = 0f;
                boxMinion.Stats.CurrentMana -= 1;
                if (boxMinion.Stats.CurrentMana <= 0)
                {
                    boxMinion.Die(CreateDeathData(false, 0, boxMinion, boxMinion, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
                }
            }
        }

        public void CheckForTargets(Minion boxMinion)
        {
            if (boxMinion == null) return;

            var units = GetUnitsInRange(boxMinion.Position, boxMinion.Stats.Range.Total - 50f, true);
            AttackableUnit nextTarget = null;
            var nextTargetPriority = ClassifyUnit.DEFAULT;

            foreach (var u in units)
            {
                if (u.IsDead || u.Team == boxMinion.Team || !u.Status.HasFlag(StatusFlags.Targetable) || u.Status.HasFlag(StatusFlags.Stealthed) || u == boxMinion)
                {
                    continue;
                }

                if (boxMinion.TargetUnit == null)
                {
                    var priority = boxMinion.ClassifyTarget(u);
                    if (priority < nextTargetPriority)
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

            if (nextTarget != null)
            {
                boxMinion.SetTargetUnit(nextTarget, true);
            }
        }
    }
}