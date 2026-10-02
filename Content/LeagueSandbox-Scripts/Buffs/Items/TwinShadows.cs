using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class ItemWraithHunt : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Minion _ghost;
        private Champion _target;
        private ObjAIBase _owner;
        private Spell _spell;
        private Buff _buff;
        private float _elapsed;
        private bool _returning;
        private Vector2? _pathDestination;
        private Particle _trail;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _ghost = (Minion)unit;
            _owner = buff.SourceUnit;
            _spell = spell;
            _buff = buff;
            _target = buff.Variables.Get<Champion>("target");
            StatsModifier.MoveSpeed.FlatBonus = 450f - _ghost.Stats.MoveSpeed.BaseValue;
            _ghost.AddStatModifier(StatsModifier);
            _ghost.UpdateMoveOrder(OrderType.MoveTo);
            _trail = AddParticleTarget(
                _owner,
                _ghost,
                "wraith_trail_green.troy",
                _ghost,
                12f,
                enemyParticle: "wraith_trail_red.troy"
            );
        }

        public void OnUpdate(float diff)
        {
            _elapsed += diff;
            if (_elapsed >= 6000f || _target == null || _target.IsDead)
            {
                _returning = true;
            }

            Vector2 destination = _returning ? _owner.Position : _target.Position;
            if (Vector2.DistanceSquared(_ghost.Position, destination) <= 100f * 100f)
            {
                if (_returning)
                {
                    _owner.Inventory.LowerActiveCooldown(_owner, _spell.SpellName, 20f);
                }
                else
                {
                    AddBuff("ItemWraithCollarSlow", 2.5f, 1, _spell, _target, _owner);
                    AddBuff("ItemWraithCollarSight", 2.5f, 1, _spell, _target, _owner);
                    AddParticleTarget(_owner, _target, "wraith_collar_tar.troy", _target, 1f);
                }

                RemoveBuff(_buff);
                return;
            }

            bool needsNewPath = !_pathDestination.HasValue || _ghost.IsPathEnded()
                || Vector2.DistanceSquared(_pathDestination.Value, destination) >= 25f * 25f;
            if (needsNewPath)
            {
                var waypoints = new List<Vector2>
                {
                    _ghost.Position,
                    destination
                };
                if (_ghost.SetWaypoints(waypoints))
                {
                    _pathDestination = destination;
                }
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_trail);
            _trail = null;
            DespawnMinion(_ghost);
        }
    }

    public class ItemWraithCollarSlow : ItemFrozenMalletSlow, IBuffGameScript
    {
        public ItemWraithCollarSlow()
        {
            BuffMetaData.IsHidden = false;
        }

        private Particle _slow;
        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            buff.Variables.Set("slow", 0.4f);
            base.OnActivate(unit, buff, spell);
            _slow = AddParticleTarget(buff.SourceUnit, unit, "global_slow.troy", unit, buff.Duration);
        }

        public override void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            base.OnDeactivate(unit, buff, spell);
            RemoveParticle(_slow);
        }
    }

    public class ItemWraithCollarSight : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Particle _watched;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ItemVisionHelpers.Reveal(buff.SourceUnit, unit, buff.Duration);

            _watched = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "global_watched.troy",
                unit,
                buff.Duration,
                bone: "C_BUFFBONE_GLB_HEAD_LOC",
                reqVision: false,
                ignoreCasterVisibility: true
            );
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_watched);
        }
    }
}
