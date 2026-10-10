using System;
using System.Linq;
using System.Numerics;
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
using ShacoScripts;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class JackInTheBoxInternal : IBuffGameScript
    {
        private Minion _box;
        private Buff _buff;
        private Spell _spell;
        private bool _armed, _triggered;
        private float _age, _activeTime, _attackTimer;
        private AttackableUnit _target;
        private const float TriggerRadius = 300;
        private const float FearRadius = 425;
        private const SpellDataFlags Enemies = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral
            | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions;

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _box = (Minion)unit;
            _buff = buff;
            _spell = ownerSpell;
            ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
        }

        public void OnUpdate(float diff)
        {
            if (_box.IsDead || _box.IsToRemove())
            {
                _buff.DeactivateBuff();
                return;
            }

            float seconds = diff / 1000;
            _age += seconds;
            if (!_armed && _age >= 2)
            {
                _armed = true;
                _box.EnterStealth();
            }

            if (!_triggered)
            {
                _box.Stats.CurrentMana = Math.Max(0, _buff.Variables.GetFloat("HiddenDuration") - Math.Max(0, _age - 2));
                if (_age >= 2 + _buff.Variables.GetFloat("HiddenDuration"))
                {
                    _buff.DeactivateBuff();
                    return;
                }
                if (_armed && !Disabled() && GetUnitsInRange(_box, _box.Position, TriggerRadius, true, Enemies)
                    .Any(unit => ShacoHelpers.CanAttack(_box, unit))) Activate();
                return;
            }

            _activeTime += seconds;
            _box.Stats.CurrentMana = Math.Max(0, 5 - _activeTime);
            if (_activeTime >= 5)
            {
                _buff.DeactivateBuff();
                return;
            }
            if (Disabled()) return;

            if (!ShacoHelpers.CanAttack(_box, _target)
                || Vector2DistanceSquared(_target, _box) > _box.Stats.Range.Total * _box.Stats.Range.Total)
            {
                _target = GetUnitsInRange(_box, _box.Position, _box.Stats.Range.Total, true, Enemies)
                    .Where(unit => ShacoHelpers.CanAttack(_box, unit))
                    .OrderByDescending(unit => unit is Champion or Pet { IsClone: true }).FirstOrDefault();
            }

            _attackTimer -= seconds;
            if (_target != null && _attackTimer <= 0)
            {
                _attackTimer = 1 / Math.Max(0.1f, _box.Stats.GetTotalAttackSpeed());
                FaceDirection(_target.Position, _box);
                SpellCast(_box, 0, SpellSlotType.SpellSlots, false, _target, Vector2.Zero, isForceCastingOrChanneling: true, overrideForceLevel: 0);
            }
        }

        private static float Vector2DistanceSquared(AttackableUnit target, AttackableUnit box)
        {
            return target == null ? float.MaxValue : System.Numerics.Vector2.DistanceSquared(target.Position, box.Position);
        }

        private bool Disabled()
        {
            return _box.HasBuff("ItemDisableShacoBox") || _box.Status.HasFlag(StatusFlags.Silenced)
                || !_box.CanAttack();
        }

        private void Activate()
        {
            _triggered = true;
            _box.ExitStealth();
            _box.Stats.ManaPoints.BaseValue = 5;
            _box.Stats.CurrentMana = 5;
            foreach (var enemy in GetUnitsInRange(_box, _box.Position, FearRadius, true, Enemies))
            {
                if (!ShacoHelpers.CanAttack(_box, enemy)) continue;
                var variables = new BuffVariables();
                variables.Set("SlowPercent", 0.4f);
                AddBuff("Fear", _buff.Variables.GetFloat("FearDuration"), 1, _spell, enemy, _box, buffVariables: variables);
            }
        }

        private void OnDeath(DeathData data)
        {
            _buff.DeactivateBuff();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnDeath.RemoveListener(this);
            if (_armed && !_triggered) _box.ExitStealth();
            DespawnMinion(_box);
        }
    }
}
