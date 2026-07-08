using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoEBlock : IBuffGameScript
    {
        private bool _doSpin = false;
        private ObjAIBase _owner;
        private Spell _spell;
        private bool _knockUp = false;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData { BuffAddType = BuffAddType.REPLACE_EXISTING };
        public StatsModifier StatsModifier { get; private set; }
        

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _doSpin = false;
            _owner = buff.SourceUnit;
            
            AddBuff("YasuoDashGhosted", 0.6f, 1, ownerSpell, _owner, _owner);

            int charLevel = _owner.Stats.Level;
            int trueELevel = charLevel >= 13 ? 5 : charLevel >= 12 ? 4 : charLevel >= 10 ? 3 : charLevel >= 8 ? 2 : 1;

            float baseDamage = 50f + (20f * trueELevel); 
            int stacks = _owner.HasBuff("YasuoDashScalar") ? _owner.GetBuffWithName("YasuoDashScalar").StackCount : 0;
            float totalEDamage = (baseDamage * (1.0f + (0.25f * stacks))) + (_owner.Stats.AbilityPower.Total * 0.6f);

            unit.TakeDamage(_owner, totalEDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, ownerSpell);
            
            AddBuff("YasuoDashScalar", 6.0f, 1, ownerSpell, _owner, _owner);

            ApiEventManager.OnMoveSuccess.AddListener(this, _owner, OnMoveSuccess, true);
            _spell = _owner.Spells[0];
            
            ApiEventManager.OnSpellPress.AddListener(this, _spell, OnSpellPress, true);
            var flash = _owner.GetSpell("SummonerFlash");
            if (flash != null) ApiEventManager.OnSpellPress.AddListener(this, flash, OnSpellPress2, true);
            
            AddParticleTarget(_owner, _owner, "Yasuo_Base_E_Dash", _owner);
            AddParticleTarget(_owner, unit, "Yasuo_Base_E_dash_hit", unit);
            
            var to = Vector2.Normalize(unit.Position - _owner.Position);
            ForceMove(_owner, new Vector2(_owner.Position.X + to.X * 375f, _owner.Position.Y + to.Y * 375f), 750f + _owner.Stats.GetTrueMoveSpeed() * 0.6f, lockActions: false);
        }

        public void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (_owner.HasBuff("YasuoDashGhosted")) _owner.RemoveBuffsWithName("YasuoDashGhosted");
            
            AddBuff("YasoAnimTest", 4f, 1, _spell, _owner, _owner);
            if (_doSpin)
            {
                _knockUp = false;

                if (_owner.HasBuff("YasuoQ3W"))
                {
                    AddParticleTarget(_owner, _owner, "yasuo_base_eq3_cas", _owner);
                    _owner.RemoveBuffsWithName("YasuoQ3W");
                    _knockUp = true;

                    int charLevel = _owner.Stats.Level;
                    int trueQLevel = charLevel >= 9 ? 5 : charLevel >= 7 ? 4 : charLevel >= 5 ? 3 : charLevel >= 4 ? 2 : 1;

                    float baseCooldown = 5.25f - (0.25f * trueQLevel);
                    float bonusAS = _owner.Stats.AttackSpeedMultiplier.Total - 1.0f; 
                    if (bonusAS < 0) bonusAS = 0;

                    float cdReduction = bonusAS / 1.67f; 
                    float finalCooldown = baseCooldown * (1f - cdReduction);
                    if (finalCooldown < 1.33f) finalCooldown = 1.33f;
                    _owner.Spells[0].SetCooldown(finalCooldown, true);
                }
                AddParticleTarget(_owner, _owner, "yasuo_base_eq_cas", _owner);
                PlayAnimation(_owner, "Spell1_Dash", timeScale: 0.8f);
                
                var timerAnm = new GameScriptTimer(0.5f, () => { StopAnimation(unit, "Spell1_Dash", StopAnimationFlags.FadeOut | StopAnimationFlags.IgnoreLock); });
                unit.RegisterTimer(timerAnm);

                // The old code used a SingleTick SpellSector purely as a "fire once if an enemy is within
                // 200" gate, then re-queried the 200 range itself in the handler. Skip the entity and run
                // the spin-damage directly — _owner is at its post-dash position here, and DoSpinDamage
                // already no-ops when no enemy is in range.
                DoSpinDamage();
            }
        }

        private void DoSpinDamage()
        {
            var enemies = EnumerateValidUnitsInRange(_owner, _owner.Position, 200f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral).ToList();
            if (enemies.Count != 0)
            {
                if (_owner.HasBuff("YasuoQ"))
                {
                    _owner.RemoveBuffsWithName("YasuoQ");
                    AddBuff("YasuoQ3W", 10f, 1, _spell, _owner, _owner);
                }
                else if (!_knockUp)
                {
                    AddBuff("YasuoQ", 10f, 1, _spell, _owner, _owner);
                }

                PlaySound("Play_sfx_Yasuo_YasuoQ_hit", _owner);
                if (_knockUp) { PlaySound("Play_sfx_Yasuo_YasuoQ3W_hit", _owner); }

                int charLevel = _owner.Stats.Level;
                int trueQLevel = charLevel >= 9 ? 5 : charLevel >= 7 ? 4 : charLevel >= 5 ? 3 : charLevel >= 4 ? 2 : 1;

                float baseDamage = 20f * trueQLevel;
                float adScaling = _owner.Stats.AttackDamage.Total;
                float totalDamage = baseDamage + adScaling;

                bool isCrit = new System.Random().NextDouble() < _owner.Stats.CriticalChance.Total;
                if (isCrit)
                {
                    float critMod = _owner.Stats.CriticalDamage.Total - 0.25f;
                    totalDamage = baseDamage + (adScaling * critMod);
                }

                AttackableUnit closestEnemy = null;
                float minDistance = float.MaxValue;
                foreach (var enemy in enemies)
                {
                    float dist = Vector2.Distance(_owner.Position, enemy.Position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestEnemy = enemy;
                    }
                }

                foreach (var enemy in enemies)
                {
                    if (enemy == closestEnemy)
                    {
                        if (_spell.CastInfo.Targets == null) _spell.CastInfo.Targets = new System.Collections.Generic.List<CastTarget>();
                        _spell.CastInfo.IsAutoAttack = true; 
                        _spell.CastInfo.SetTarget(enemy, 0);

                        ApiEventManager.OnLaunchAttack.Publish(_owner, _spell);

                        enemy.TakeDamage(_owner, totalDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, isCrit, _spell);
                        
                        _spell.CastInfo.IsAutoAttack = false;
                    }
                    else
                    {
                        enemy.TakeDamage(_owner, totalDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, isCrit, _spell);
                    }

                    if (_knockUp) { AddBuff("YasuoQ3Mis", 1.2f, 1, _spell, enemy, _owner); }
                }
            }
        }

        public void OnSpellPress(Spell sp, SpellCastInfo sc) { _doSpin = true; }
        public void OnSpellPress2(Spell sp, SpellCastInfo sc) { sp.CastInfo.Owner.SetForceMovementState(false); sp.Cast(sc.Position, sc.EndPosition, sc.TargetUnit); }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
    }
}
