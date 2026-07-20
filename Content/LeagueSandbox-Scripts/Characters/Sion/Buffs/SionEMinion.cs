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
        private List<DamageData> _deferredDamage = [];

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();


        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            _sion = buff.SourceUnit;
            _buff = buff;
            _hitUnits.Add(unit);

            var start = buff.BuffVars.Get("start", Vector2.Zero);
            var end = buff.BuffVars.Get("end", Vector2.Zero);
            var dirVec = end - start;
            var dir = dirVec.LengthSquared() > 0.001f ? Vector2.Normalize(dirVec) : new Vector2(1, 0);
            var flingTarget = start + dir * 1350f;

            SpellEffectCreate("Sion_Base_E_Minion.troy",_sion, unit, unit, lifetime: buff.Duration, scale: 2f,
                flags: FXFlags.SimulateWhileOffScreen);
            ForceMove(unit, flingTarget, 2100f, gravity: 0f, resolve: ForceMovementType.FIRST_COLLISION_HIT,
                facing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
                orders: ForceMovementOrdersType.POSTPONE_CURRENT_ORDER, movementName: "SionEMinion");

            AddBuff("SionESoundMinionColide", 0.25f, 1, ownerSpell, unit, _sion);
            AddBuff("Stun", 0.75f, 1, ownerSpell, unit, _sion);


            ApiEventManager.OnPreTakeDamage.AddListener(this, unit, OnPreTakeDamage);
            var damage = ownerSpell.SpellData.EffectLevelAmount[1][ownerSpell.CastInfo.SpellLevel]
                         + _sion.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
            unit.TakeDamage(_sion, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
            _bubble = AddUnitPerceptionBubble(_sion, 160f, buff.Duration, _sion.Team);
            ApiEventManager.OnMoveEnd.AddListener(this, unit, OnMoveEnd);
        }

        private void OnPreTakeDamage(DamageData data)
        {
            // Bug fix: DamageData is a reference type, so storing "data" and then zeroing it
            // below would also zero the entry in _deferredDamage (same instance) and the damage
            // would be swallowed instead of deferred. Snapshot the values first.
            _deferredDamage.Add(new DamageData
            {
                Attacker = data.Attacker,
                CallForHelpAttacker = data.CallForHelpAttacker,
                Target = data.Target,
                Damage = data.Damage,
                PostMitigationDamage = data.PostMitigationDamage,
                DamageType = data.DamageType,
                DamageSource = data.DamageSource,
                DamageResultType = data.DamageResultType,
                IsAutoAttack = data.IsAutoAttack,
                ForceCallForHelp = data.ForceCallForHelp,
            });
            data.Damage = 0;
            data.PostMitigationDamage = 0;
            data.DamageResultType = DamageResultType.RESULT_INVULNERABLENOMESSAGE;
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
                SpellEffectCreate("Sion_Base_E_Tar.troy", _sion, target, target, orientTowards: target.GetPosition3D(),
                    flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);

                AddBuff("SionESlow", 2.5f, 1, buff.OriginSpell, target, _sion);
                target.TakeDamage(_sion,
                    (buff.OriginSpell.SpellData.EffectLevelAmount[1][buff.OriginSpell.CastInfo.SpellLevel]
                     + buff.OriginSpell.SpellData.Coefficient * _sion.Stats.AbilityPower.Total) * 2,
                    DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                    DamageResultType.RESULT_NORMAL);
                _hitUnits.Add(target);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnPreTakeDamage.RemoveListener(this, unit);
            foreach (var damageDate in _deferredDamage)
            {
                unit.TakeDamage(damageDate.Attacker, damageDate.Damage, damageDate.DamageType, damageDate.DamageSource,
                    damageDate.DamageResultType);
            }

            _hitUnits.Clear();
            _bubble.SetToRemove();
            _deferredDamage.Clear();
        }
    }
}