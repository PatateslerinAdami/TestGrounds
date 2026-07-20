using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using System.Numerics;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class SionWShieldStacks : IBuffGameScript
    {
        private ObjAIBase _sion;
        private Shield _soulFurnaceShield;
        private Particle _shieldParticle;
        private Particle _soundLoop;
        private Buff _buff;
        private float _lastShieldValue;
        private PeriodicTicker _periodicTicker;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COUNTER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            // COUNTER buff → travels as int32 via NPC_BuffUpdateNumCounter, so no 254 byte-cap
            // applies (Buff.cs:104). Sion's shield can scale into very large values, so allow the
            // full int range rather than an arbitrary ceiling.
            MaxStacks = int.MaxValue,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _sion = buff.SourceUnit;
            _buff = buff;
            
            _soundLoop = SpellEffectCreate("Sion_Base_W_Sound.troy",_sion, _sion,  _sion, lifetime:_buff.Duration + 0.5f, scale: 1.3f, boneName: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.SimulateWhileOffScreen);
            _shieldParticle = SpellEffectCreate("Sion_Base_W_Shield.troy",_sion, _sion,  _sion, lifetime: _buff.Duration, scale: 0.5f, boneName: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.SimulateWhileOffScreen);
            
            var hpScaling = _sion.Stats.HealthPoints.Total * ownerSpell.SpellData.EffectLevelAmount[6][ownerSpell.CastInfo.SpellLevel]/100;
            var ap =_sion.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
            var shieldValue = ownerSpell.SpellData.EffectLevelAmount[1][ownerSpell.CastInfo.SpellLevel] + ap + hpScaling;
            _lastShieldValue = shieldValue;
            _soulFurnaceShield = AddShield(_sion, _sion, shieldValue, true, true);
            UpdateDisplay();
            ApiEventManager.OnShieldBreak.AddListener(this, _soulFurnaceShield, OnShieldBreak);
            ApiEventManager.OnShieldReduced.AddListener(this, _soulFurnaceShield, OnShieldReduce);

            SetSpell(_sion, "SionWDetonate", SpellSlotType.SpellSlots, 1, changeFlags: 0x6E);
            _sion.Spells[1].SetCooldown(0f, true);
            SealSpellSlot(_sion, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
        }

        public void OnUpdate(Buff buff, float diff)
        {
            // Soul Furnace can't be recast for the first 2s (wiki; replay min recast gap = 2.18s,
            // clean cluster from ~2.0s). Unseal the detonate slot once that lockout elapses.
            ExecutePeriodically(buff.BuffVars, "SoulFurnaceRecastTimer", 2000f, false, 1, () =>
            {
                SealSpellSlot(_sion, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
            });
        }

        private void OnShieldReduce(Shield shield, float amount)
        {
            _lastShieldValue = _soulFurnaceShield.Amount;
           UpdateDisplay();
        }

        private void OnShieldBreak(Shield shield)
        { 
            RemoveBuff(_buff);
        }

        private void UpdateDisplay() {
            // COUNTER buff: the shield amount is replay-verified to exceed 255 (reaches 500+), so it
            // must travel as int32 via NPC_BuffUpdateNumCounter, not the byte BuffUpdateCount packet.
            EditBuffCounter(_buff, (int)_soulFurnaceShield.Amount);
        }


        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            AddBuff("SionWSoundExplosion", 0.25f, 1, ownerSpell, _sion, _sion);

            if (_soulFurnaceShield.Amount > 0)
            {
                PlayAnimation(_sion, "Spell2_B", 0, 0, 1, AnimationFlags.NoBlend | AnimationFlags.Junk5 | AnimationFlags.Junk7);
                SpellEffectCreate("Sion_Base_W_Nova.troy",_sion, null, null,_sion.Position, _sion.Position, scale: 1.3f, flags:  FXFlags.SimulateWhileOffScreen);
                var unitsInRange = ForEachUnitInTargetAreaAddBuff(_sion, _sion.Position, 500f,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                    SpellDataFlags.AffectHeroes, "SionWSoundHit", 0.25f, 1, _sion, ownerSpell);

                var ap = _sion.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
                var dmg = ownerSpell.SpellData.EffectLevelAmount[2][ownerSpell.CastInfo.SpellLevel] + ap;
                foreach (var unitInRange in unitsInRange)
                {
                    var maxHealthDmg = unitInRange.Stats.HealthPoints.Total * (ownerSpell.SpellData.EffectLevelAmount[6][ownerSpell.CastInfo.SpellLevel]/100) + dmg;
                    
                    SpellEffectCreate("Sion_Base_W_Nova_Tar.troy",_sion,  null, null,unitInRange.Position, _sion.Position, scale: 1.3f, orientTowards: unitInRange.GetPosition3D(), flags: FXFlags.UpdateOrientation |  FXFlags.SimulateWhileOffScreen);
                    unitInRange.TakeDamage(_sion, maxHealthDmg,
                        DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                        DamageResultType.RESULT_NORMAL);
                }
            }
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveShield(_sion, _soulFurnaceShield);
            RemoveParticle(_shieldParticle);
            RemoveParticle(_soundLoop);

            SealSpellSlot(_sion, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
            SetSpell(_sion, "SionW", SpellSlotType.SpellSlots, 1);
            _sion.Spells[1].SetCooldown(_sion.Spells[1].GetCooldown(), false);
        }
    }
}