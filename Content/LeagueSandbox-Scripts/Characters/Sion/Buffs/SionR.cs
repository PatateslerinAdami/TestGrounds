using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSandbox.GameServer.API;

namespace Buffs
{
    internal class SionR : IBuffGameScript
    {
        private Particle _p1, _p2, _p3;
        private float MaxSpeed = 950f;
        private float _currentBonusSpeed = 0f;
        private bool _animationIsFast = false;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };

        public StatsModifier StatsModifier { get; set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            SetCharacterVoiceOverride(unit, "Max");
            MaxSpeed = Math.Max(0, 950f - unit.GetMoveSpeed());
            SetStatus(unit, StatusFlags.Ghosted, true);
            _p1 = SpellEffectCreate("Sion_Base_R_GlobalSound.troy", unit, unit, unit, boneName: "Root", flags: FXFlags.SimulateWhileOffScreen);
            _p2 = SpellEffectCreate("Sion_Base_R_Cas.troy",unit, unit,  null, lifetime: buff.Duration, scale: 2f, flags: FXFlags.SimulateWhileOffScreen);
            _p3 = SpellEffectCreate("Sion_Base_R_Skin.troy", unit, unit, unit, flags: FXFlags.SimulateWhileOffScreen);
            
            ApiEventManager.OnAllowAddBuff.AddListener(this, unit, OnAllowAddBuff);
            unit.SetAnimStates(new Dictionary<string, string>
            {
                { "RUN", "Spell4_RunIN" },
                { "RUN_FAST", "Passive_Run_Raw" },
                { "RUN_HASTE", "Passive_Run" }
            });
            //OverrideAnimation(unit, "spell4_run", "run", this);
        }

        private bool OnAllowAddBuff(AttackableUnit target, AttackableUnit unit, Buff buff)
        {
            if (buff.BuffType is not (BuffType.BLIND or BuffType.CHARM or BuffType.DISARM or BuffType.FEAR
                or BuffType.FLEE
                or BuffType.FRENZY or BuffType.KNOCKBACK or BuffType.KNOCKUP or BuffType.NEAR_SIGHT
                or BuffType.POLYMORPH
                or BuffType.SLEEP or BuffType.SILENCE or BuffType.SLOW or BuffType.SNARE or BuffType.STUN
                or BuffType.TAUNT
                or BuffType.SUPPRESSION or BuffType.HASTE)) return true;
            Say(unit, "game_lua_SpellImmunity");
            return false;
        }

        public void OnUpdate(Buff buff, float diff)
        {
            if (_currentBonusSpeed >= MaxSpeed) return;
            ExecutePeriodically(buff.BuffVars, "SionRSpeed", 150f, false, 0, () =>
            {
                {
                    // Ramp a fixed +40 per step, clamped so the last step lands EXACTLY on MaxSpeed
                    // (= 950 - base true speed) instead of overshooting. Applied as a soft-cap-EXEMPT
                    // flat bonus so the charge reaches a true 950 — a normal MoveSpeed modifier would
                    // be halved by the >490 soft cap. Set directly (no StatsModifier add/remove churn),
                    // which recomputes + replicates the true speed immediately.
                    _currentBonusSpeed = Math.Min(_currentBonusSpeed + 40, MaxSpeed);
                    buff.TargetUnit.Stats.MoveSpeedSoftCapExemptFlatBonus = _currentBonusSpeed;
                    if (_animationIsFast) return;
                    if (!(_currentBonusSpeed >= MaxSpeed / 2)) return;
                    buff.TargetUnit.SetAnimStates(new Dictionary<string, string>
                    {
                        { "RUN", "" },
                        { "RUN_FAST", "" },
                        { "RUN_HASTE", "" }
                    });
                    buff.TargetUnit.SetAnimStates(new Dictionary<string, string>
                    {
                        { "RUN", "Spell4_Run_BASE" },
                        { "RUN_FAST", "Passive_Run_Raw" },
                        { "RUN_HASTE", "Passive_Run" }
                    });
                    _animationIsFast = true;
                }
            });
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ResetCharacterVoiceOverride(unit);
            // Clear the soft-cap-exempt charge speed (mirrors the StatsModifier removal it replaced).
            unit.Stats.MoveSpeedSoftCapExemptFlatBonus = 0f;
            SetStatus(unit, StatusFlags.Ghosted, false);
            RemoveParticle(_p1);
            RemoveParticle(_p2);
            RemoveParticle(_p3);
            unit.SetAnimStates(new Dictionary<string, string>
            {
                { "RUN", "" },
                { "RUN_FAST", "" },
                { "RUN_HASTE", "" }
            });
            ApiEventManager.OnAllowAddBuff.RemoveListener(this, unit, OnAllowAddBuff);
        }
    }
}