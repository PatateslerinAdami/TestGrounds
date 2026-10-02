using System.Linq;
using System.Collections.Generic;
using System.Numerics;
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
    public class ItemRighteousGlory : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Particle _warning;
        private Buff _buff;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _buff = buff;

            PlaySound("Play_sfx_ItemRighteousGlory_OnBuffCast", unit);
            PlaySound("Play_sfx_ItemRighteousGlory_OnBuffActivate", unit);
            _warning = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "global_item_righteousglory_preaoe.troy",
                unit,
                buff.Duration,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
        }

        public void OnUpdate(float diff)
        {
            if (_buff.TargetUnit.IsDead)
            {
                RemoveBuff(_buff);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            PlaySound("Stop_sfx_ItemRighteousGlory_OnBuffActivate", unit);
            RemoveParticle(_warning);
            var recipients = buff.Variables.Get<List<AttackableUnit>>("recipients");
            foreach (var ally in recipients ?? EnumerateUnitsInRange(unit.Position, 3000, false).ToList())
            {
                var hasteBuff = ally.GetBuffWithName("ItemRighteousGlorySpeed");
                if (hasteBuff != null && hasteBuff.SourceUnit == buff.SourceUnit)
                {
                    RemoveBuff(ally, "ItemRighteousGlorySpeed");
                }
            }

            if (unit.IsDead)
            {
                return;
            }

            PlaySound("Play_sfx_ItemRighteousGlory_OnBuffDeactivate", unit);
            AddParticleTarget(
                buff.SourceUnit,
                unit,
                "global_item_righteousglory_aoe.troy",
                unit,
                1.0f,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
            foreach (var target in EnumerateUnitsInRange(unit.Position, 450, true).OfType<Champion>())
            {
                if (target.Team != unit.Team)
                {
                    var variables = new BuffVariables();
                    variables.Set("slow", 0.8f);
                    AddBuff("CrippleSlow", 1, 1, spell, target, buff.SourceUnit, buffVariables: variables);
                    AddParticleTarget(
                        buff.SourceUnit,
                        target,
                        "global_item_righteousglory_tar.troy",
                        target,
                        1.0f,
                        ignoreCasterVisibility: true
                    );
                }
            }
        }
    }

    public class ItemRighteousGlorySpeed : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HASTE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _unit;
        private Buff _buff;
        private Particle _speed;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _unit = (ObjAIBase)unit;
            _buff = buff;
            var castParticle = unit == buff.SourceUnit ? "global_item_righteousglory_cas.troy" : "global_item_righteousglory_allycas.troy";
            AddParticleTarget(buff.SourceUnit, unit, castParticle, unit, 1.0f, ignoreCasterVisibility: true);
            OnUpdate(0);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_speed);
        }

        public void OnUpdate(float diff)
        {
            bool towardsEnemy = !_unit.IsDead && _unit.Waypoints.Count > 1 && EnumerateUnitsInRange(
                _unit.Position,
                2000,
                true
            ).Any(enemy => enemy.Team != _unit.Team && (enemy is Champion || enemy is BaseTurret)
                && Vector2.DistanceSquared(_unit.Waypoints.Last(), enemy.Position) < Vector2.DistanceSquared(_unit.Position, enemy.Position));
            float speed = towardsEnemy ? 0.6f : 0f;
            if (towardsEnemy && _speed == null)
            {
                _speed = AddParticleTarget(
                    _buff.SourceUnit,
                    _unit,
                    "global_item_righteousglory_speedbuff.troy",
                    _unit,
                    System.Math.Max(0.0f, _buff.Duration - _buff.TimeElapsed),
                    bone: "C_BUFFBONE_GLB_GROUND_LOC",
                    ignoreCasterVisibility: true
                );
            }
            else if (!towardsEnemy && _speed != null)
            {
                RemoveParticle(_speed);
                _speed = null;
            }

            if (StatsModifier.MoveSpeed.PercentBonus == speed)
            {
                return;
            }

            _unit.RemoveStatModifier(StatsModifier);
            StatsModifier.MoveSpeed.PercentBonus = speed;
            _unit.AddStatModifier(StatsModifier);
        }
    }

    public class ItemRighteousGloryHaste : ItemRighteousGlorySpeed
    {
    }

    public class ItemRighteousGlorySlow : ItemFrozenMalletSlow
    {
        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            buff.Variables.Set("slow", 0.8f);
            base.OnActivate(unit, buff, spell);
        }
    }
}
