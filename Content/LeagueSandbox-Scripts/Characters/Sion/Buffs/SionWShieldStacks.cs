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
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COUNTER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1000000,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _sion = ownerSpell.CastInfo.Owner;
            _buff = buff;
            // Wire (fxdump audit): W_Shield sits on C_Buffbone_Glb_Center_Loc (0x0ac0f283), scale 0.5;
            // Sion_Base_W_Sound.troy is the shield LOOP sound — spawned at CAST, FX_Killed when the
            // shield ends (observed lifetimes 1.07-5.43s = exactly the hold time), NOT an explosion FX.
            _shieldParticle = AddParticleTarget(_sion, _sion, "Sion_Base_W_Shield.troy", _sion, _buff.Duration, size: 0.5f, bone: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.SimulateWhileOffScreen);
            _soundLoop = AddParticleTarget(_sion, _sion, "Sion_Base_W_Sound.troy", _sion, _buff.Duration + 0.5f, size: 1.3f, bone: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.SimulateWhileOffScreen);
            var hpScaling = _sion.Stats.HealthPoints.Total * 0.1f;
            var ap =_sion.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
            var shieldValue = ownerSpell.SpellData.EffectLevelAmount[1][ownerSpell.CastInfo.SpellLevel] + ap + hpScaling;
            _lastShieldValue = shieldValue;
            _soulFurnaceShield = AddShield(_sion, _sion, shieldValue, true, true);
            UpdateDisplay();
            ApiEventManager.OnShieldBreak.AddListener(this, _soulFurnaceShield, OnShieldBreak);
            ApiEventManager.OnShieldReduced.AddListener(this, _soulFurnaceShield, OnShieldReduce);
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
            // Wire: the explosion-sound buff is added at EVERY shield end — recast-detonate,
            // shield break AND expiry (test replay: 60 SionWSoundExplosion adds vs 59 shield
            // ends). Single-sourced HERE so all end paths sound identical; SionWDetonate must
            // NOT add it again.
            AddBuff("SionWSoundExplosion", 0.25f, 1, ownerSpell, _sion, _sion);

            if (_soulFurnaceShield.Amount > 0)
            {
                AddParticlePos(_sion, "Sion_Base_W_Nova.troy", _sion.Position, _sion.Position, size: 1.3f, flags:  FXFlags.SimulateWhileOffScreen);
                var unitsInRange = ForEachUnitInTargetAreaAddBuff(_sion, _sion.Position, 500f,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                    SpellDataFlags.AffectHeroes, "SionWSoundHit", 0.25f, 1, _sion, ownerSpell);
                
                foreach (var unitInRange in unitsInRange)
                {
                    AddParticlePos(_sion, "Sion_Base_W_Nova_Tar.troy", unitInRange.Position, _sion.Position, size: 1.3f, flags: FXFlags.UpdateOrientation |  FXFlags.SimulateWhileOffScreen);
                    unitInRange.TakeDamage(_sion,
                        ownerSpell.SpellData.EffectLevelAmount[2][ownerSpell.CastInfo.SpellLevel],
                        DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                        DamageResultType.RESULT_NORMAL);
                }
            }
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveShield(_sion, _soulFurnaceShield);
            RemoveParticle(_shieldParticle);
            // FX_Kill for the shield loop sound at the end tick (wire-verified pattern).
            RemoveParticle(_soundLoop);
            RemoveBuff(_sion, "SionW");
        }
    }
}