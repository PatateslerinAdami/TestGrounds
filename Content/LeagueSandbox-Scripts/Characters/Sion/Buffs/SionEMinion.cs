using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using System.Numerics;
using GameMaths;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class SionEMinion : IBuffGameScript
    {
        private ObjAIBase _sion;
        private AttackableUnit _unit;
        private Spell _spell;
        private Buff _buff;
        private Spell _mainSpell;
        private Region _bubble;
        private HashSet<AttackableUnit> _hitUnits = [];
        private float _deferredDamage;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _spell = ownerSpell;
            _unit = unit;
            _sion = buff.SourceUnit;
            _buff = buff;
            _mainSpell = _sion.Spells[2];
            _hitUnits.Add(unit);
            // Wire-derived (test replay, 23 clean flings + 6 terrain stops): the fling target is
            // the point ON THE MISSILE RAY at 1350 units from the CAST ORIGIN — NOT "1350 further
            // from the hit position" and NOT radially away from the minion (end points lie within
            // ~1u lateral of the ray even for minions hit up to ~120u off-axis; the radial model
            // mispredicts by ~220u). So knockback length = 1350 - hitDistance. Terrain stops the
            // flight early (observed 516/601/901/1006/1176); full flights end at 1272-1347 from
            // the cast origin (navgrid snap), never beyond 1350. Speed 2100, gravity 0 (wire).
            var start = buff.BuffVars.Get("start", Vector2.Zero);
            var aim = buff.BuffVars.Get("end", Vector2.Zero);
            var dirVec = aim - start;
            var dir = dirVec.LengthSquared() > 0.001f ? Vector2.Normalize(dirVec) : new Vector2(1, 0);
            var flingTarget = start + dir * 1350f;
            ForceMove(unit, flingTarget, 2100f, gravity: 0f, resolve: ForceMovementType.FIRST_WALL_HIT,
                facing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
                orders: ForceMovementOrdersType.POSTPONE_CURRENT_ORDER, movementName: "SionEMinion");

            // Wire: the flung minion gets a visible generic "Stun" (0.75s) at fling start plus
            // the hidden collide-sound carrier (0.25s), on every fling in the test replay.
            AddBuff("Stun", 0.75f, 1, ownerSpell, unit, _sion);
            AddBuff("SionESoundMinionColide", 0.25f, 1, ownerSpell, unit, _sion);

            // While airborne the flung unit is immune to minion damage, and if E's own damage
            // would kill it, that damage is withheld until the fling stops (terrain, end of
            // trajectory, or interruption) so the body completes the flight.
            var damage = _mainSpell.SpellData.EffectLevelAmount[0][_mainSpell.CastInfo.SpellLevel]
                         + _mainSpell.SpellData.Coefficient * _sion.Stats.AbilityPower.Total;
            var postMitigation = unit.Stats.GetPostMitigationDamage(damage, DamageType.DAMAGE_TYPE_MAGICAL, _sion);
            if (postMitigation >= unit.Stats.CurrentHealth)
            {
                _deferredDamage = damage;
            }
            else
            {
                unit.TakeDamage(_sion, damage, DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
            }
            ApiEventManager.OnPreTakeDamage.AddListener(this, unit, OnPreTakeDamage);
            _bubble = AddUnitPerceptionBubble(_sion, 160f, buff.Duration, _sion.Team);
            ApiEventManager.OnMoveEnd.AddListener(this, unit, OnMoveEnd);
        }

        private void OnPreTakeDamage(DamageData data)
        {
            if (data.Attacker is Minion)
            {
                data.Damage = 0;
                data.PostMitigationDamage = 0;
            }
        }

        private void OnMoveEnd(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters.MovementName != "SionEMinion") return;
            RemoveBuff(_buff);
        }

        public void OnUpdate(Buff buff, float diff)
        {
            var targetsInRange = ForEachUnitInTargetArea(_sion, _unit.Position, 130f,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectHeroes);
            foreach (var target in targetsInRange)
            {
                if (_hitUnits.Contains(target)) continue;
                AddBuff("SionESlow", 2.5f, 1, _spell, target, _sion);
                // Pass-through units receive E's own damage (Effect1 = 70-210 + 0.4 AP; Effect2 is
                // the slow% row consumed by SionESlow, which the old code misused here as damage).
                target.TakeDamage(_sion,
                    _mainSpell.SpellData.EffectLevelAmount[0][_mainSpell.CastInfo.SpellLevel]
                    + _mainSpell.SpellData.Coefficient * _sion.Stats.AbilityPower.Total,
                    DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
                _hitUnits.Add(target);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnPreTakeDamage.RemoveListener(this, unit);
            // Deferred lethal E damage lands now that the displacement is over (the buff is
            // removed on move end, interruption, or duration expiry).
            if (_deferredDamage > 0 && !unit.IsDead)
            {
                unit.TakeDamage(_sion, _deferredDamage, DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
            }
            _hitUnits.Clear();
            _bubble.SetToRemove();
        }
    }
}