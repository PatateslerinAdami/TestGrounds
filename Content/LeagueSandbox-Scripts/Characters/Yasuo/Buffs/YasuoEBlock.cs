using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoEBlock : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData { BuffAddType = BuffAddType.REPLACE_EXISTING };
        public StatsModifier StatsModifier { get; private set; }
        private SpellSector _targetZone;
        bool doSpin = false;
        ObjAIBase owner;
        Spell origSpell;
        bool knockUp = false;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            doSpin = false;
            owner = ownerSpell.CastInfo.Owner;
            
            AddBuff("YasuoDashGhosted", 0.6f, 1, ownerSpell, owner, owner);

            int charLevel = owner.Stats.Level;
            int trueELevel = charLevel >= 13 ? 5 : charLevel >= 12 ? 4 : charLevel >= 10 ? 3 : charLevel >= 8 ? 2 : 1;

            float baseDamage = 50f + (20f * trueELevel); 
            int stacks = owner.HasBuff("YasuoDashScalar") ? owner.GetBuffWithName("YasuoDashScalar").StackCount : 0;
            float totalEDamage = (baseDamage * (1.0f + (0.25f * stacks))) + (owner.Stats.AbilityPower.Total * 0.6f);

            unit.TakeDamage(owner, totalEDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, ownerSpell);
            
            AddBuff("YasuoDashScalar", 6.0f, 1, ownerSpell, owner, owner);

            ApiEventManager.OnMoveSuccess.AddListener(this, owner, OnMoveSuccess, true);
            origSpell = owner.Spells[0];
            
            ApiEventManager.OnSpellPress.AddListener(this, origSpell, OnSpellPress, true);
            var flash = owner.GetSpell("SummonerFlash");
            if (flash != null) ApiEventManager.OnSpellPress.AddListener(this, flash, OnSpellPress2, true);
            
            AddParticleTarget(owner, owner, "Yasuo_Base_E_Dash", owner);
            AddParticleTarget(owner, unit, "Yasuo_Base_E_dash_hit", unit);
            
            var to = Vector2.Normalize(unit.Position - owner.Position);
            ForceMovement(owner, "Spell3", new Vector2(owner.Position.X + to.X * 375f, owner.Position.Y + to.Y * 375f), 750f + owner.Stats.GetTrueMoveSpeed() * 0.6f, 0, 0, 0, consideredAsCC: false);
        }

        public void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (owner.HasBuff("YasuoDashGhosted")) owner.RemoveBuffsWithName("YasuoDashGhosted");
            
            AddBuff("YasoAnimTest", 4f, 1, origSpell, owner, owner);
            if (doSpin)
            {
                knockUp = false;

                if (owner.HasBuff("YasuoQ3W"))
                {
                    AddParticleTarget(owner, owner, "yasuo_base_eq3_cas", owner);
                    owner.RemoveBuffsWithName("YasuoQ3W");
                    knockUp = true;

                    int charLevel = owner.Stats.Level;
                    int trueQLevel = charLevel >= 9 ? 5 : charLevel >= 7 ? 4 : charLevel >= 5 ? 3 : charLevel >= 4 ? 2 : 1;

                    float baseCooldown = 5.25f - (0.25f * trueQLevel);
                    float bonusAS = owner.Stats.AttackSpeedMultiplier.Total - 1.0f; 
                    if (bonusAS < 0) bonusAS = 0;

                    float cdReduction = bonusAS / 1.67f; 
                    float finalCooldown = baseCooldown * (1f - cdReduction);
                    if (finalCooldown < 1.33f) finalCooldown = 1.33f;
                    owner.Spells[0].SetCooldown(finalCooldown, true);
                }
                AddParticleTarget(owner, owner, "yasuo_base_eq_cas", owner);
                PlayAnimation(owner, "Spell1_Dash", timeScale: 0.8f);
                
                var timerAnm = new GameScriptTimer(0.5f, () => { StopAnimation(unit, "Spell1_Dash", fade: true); });
                unit.RegisterTimer(timerAnm);

                _targetZone = origSpell.CreateSpellSector(new SectorParameters
                {
                    Type = SectorType.Area, BindObject = owner, Length = 200f,
                    Tickrate = 30, Lifetime = 2f, SingleTick = true,
                    OverrideFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes,
                });
                ApiEventManager.OnSpellSectorHit.AddListener(this, _targetZone, OnTargetZoneHit, true);
            }
        }

        public void OnTargetZoneHit(SpellSector sector, AttackableUnit target)
        {
            var enemies = EnumerateValidUnitsInRange(owner, owner.Position, 200f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral).ToList();
            if (enemies.Count != 0)
            {
                if (owner.HasBuff("YasuoQ"))
                {
                    owner.RemoveBuffsWithName("YasuoQ");
                    AddBuff("YasuoQ3W", 10f, 1, origSpell, owner, owner);
                }
                else if (!knockUp)
                {
                    AddBuff("YasuoQ", 10f, 1, origSpell, owner, owner);
                }

                PlaySound("Play_sfx_Yasuo_YasuoQ_hit", owner);
                if (knockUp) { PlaySound("Play_sfx_Yasuo_YasuoQ3W_hit", owner); }

                int charLevel = owner.Stats.Level;
                int trueQLevel = charLevel >= 9 ? 5 : charLevel >= 7 ? 4 : charLevel >= 5 ? 3 : charLevel >= 4 ? 2 : 1;

                float baseDamage = 20f * trueQLevel;
                float adScaling = owner.Stats.AttackDamage.Total;
                float totalDamage = baseDamage + adScaling;

                bool isCrit = new System.Random().NextDouble() < owner.Stats.CriticalChance.Total;
                if (isCrit)
                {
                    float critMod = owner.Stats.CriticalDamage.Total - 0.25f;
                    totalDamage = baseDamage + (adScaling * critMod);
                }

                AttackableUnit closestEnemy = null;
                float minDistance = float.MaxValue;
                foreach (var enemy in enemies)
                {
                    float dist = Vector2.Distance(owner.Position, enemy.Position);
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
                        if (origSpell.CastInfo.Targets == null) origSpell.CastInfo.Targets = new System.Collections.Generic.List<CastTarget>();
                        origSpell.CastInfo.IsAutoAttack = true; 
                        origSpell.CastInfo.SetTarget(enemy, 0);

                        ApiEventManager.OnLaunchAttack.Publish(owner, origSpell);

                        enemy.TakeDamage(owner, totalDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, isCrit, origSpell);
                        
                        origSpell.CastInfo.IsAutoAttack = false;
                    }
                    else
                    {
                        enemy.TakeDamage(owner, totalDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, isCrit, origSpell);
                    }

                    if (knockUp) { AddBuff("YasuoQ3Mis", 1.2f, 1, origSpell, enemy, owner); }
                }
            }
        }

        public void OnSpellPress(Spell sp, SpellCastInfo sc) { doSpin = true; }
        public void OnSpellPress2(Spell sp, SpellCastInfo sc) { sp.CastInfo.Owner.SetDashingState(false); sp.Cast(sc.Position, sc.EndPosition, sc.TargetUnit); }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
    }
}
