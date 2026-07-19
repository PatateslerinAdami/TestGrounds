using System;
using System.Collections.Generic;
using System.Linq;
using GameMaths;
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
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using System.Numerics;

namespace Buffs
{
    internal class SionEMinion : IBuffGameScript
    {
        private ObjAIBase _sion;
        private AttackableUnit _unit;
        private Buff _buff;
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
            _unit = unit;
            _sion = buff.SourceUnit;
            _buff = buff;
            _hitUnits.Add(unit);

            var start = buff.BuffVars.Get("start", Vector2.Zero);
            var aim = buff.BuffVars.Get("end", Vector2.Zero);
            var dirVec = aim - start;
            var dir = dirVec.LengthSquared() > 0.001f ? Vector2.Normalize(dirVec) : new Vector2(1, 0);
            var flingTarget = start + dir * 1350f;

            ForceMove(unit, flingTarget, 2100f, gravity: 0f, resolve: ForceMovementType.FIRST_COLLISION_HIT,
                facing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
                orders: ForceMovementOrdersType.POSTPONE_CURRENT_ORDER, movementName: "SionEMinion");
            
            AddBuff("SionESoundMinionColide", 0.25f, 1, ownerSpell, unit, _sion);
            AddBuff("Stun", 0.75f, 1, ownerSpell, unit, _sion);
            
            var damage = ownerSpell.SpellData.EffectLevelAmount[0][ownerSpell.CastInfo.SpellLevel]
                         + ownerSpell.SpellData.Coefficient * _sion.Stats.AbilityPower.Total;
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
            foreach (var target in targetsInRange.Where(target => !_hitUnits.Contains(target)))
            {
                AddBuff("SionESlow", 2.5f, 1, buff.OriginSpell, target, _sion);
                target.TakeDamage(_sion,
                    buff.OriginSpell.SpellData.EffectLevelAmount[0][buff.OriginSpell.CastInfo.SpellLevel]
                    + buff.OriginSpell.SpellData.Coefficient * _sion.Stats.AbilityPower.Total,
                    DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
                _hitUnits.Add(target);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnPreTakeDamage.RemoveListener(this, unit);
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