using System;
using System.Numerics;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class OdinCaptureChannelBomb : ISpellScript
    {
        private const float ChannelDuration = 4.5f;
        private const float WindupDuration = 1.25f;

        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true,
            CooldownIsAffectedByCDR = false,
            DoesntBreakShields = true,
            ChannelDuration = ChannelDuration
        };

        private Particle _beamParticle;
        private Particle _engagedBeamParticle;
        private Champion _owner;
        private Spell _spell;
        private Minion _captureMinion;
        private CharScriptOdinCenterRelic _relic;
        private Buff _captureBuff;
        private Buff _suppressionBuff;
        private bool _capturing;
        private bool _engaged;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnCanCast.AddListener(this, owner, (unit, cast) =>
            {
                return cast != spell || !unit.HasBuff("OdinCaptureChannelCooldownBuff");
            });
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var relic = (target as Minion)?.CharScript as CharScriptOdinCenterRelic;
            if (!(owner is Champion champion) || owner.HasBuff("OdinCaptureChannelCooldownBuff")
                || relic == null || !relic.CanCapture(champion))
            {
                spell.CancelCast();
            }
        }

        public void OnSpellCast(Spell spell)
        {
            _owner = spell.CastInfo.Owner as Champion;
            _spell = spell;
            _captureMinion = spell.CastInfo.Targets.Count > 0 ? spell.CastInfo.Targets[0].Unit as Minion : null;
            _relic = _captureMinion?.CharScript as CharScriptOdinCenterRelic;
            _capturing = _relic != null && _relic.BeginCapture(_owner, spell);
            _engaged = false;
            if (!_capturing)
            {
                return;
            }

            _beamParticle = AddParticleTarget(
                _owner, _owner, "OdinCaptureBeam.troy", _captureMinion, ChannelDuration,
                boneNameHash: 4929107, targetBoneNameHash: 8024133
            );
            _captureBuff = AddBuff("OdinCaptureChannel", ChannelDuration, 1, spell, _owner, _owner);
            ApiEventManager.OnTakeDamage.AddListener(this, _owner, OnDamage);
        }

        public void OnSpellChannel(Spell spell)
        {
            if (!_capturing)
            {
                spell.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.ChannelingCondition);
            }
        }

        private void OnDamage(DamageData data)
        {
            if (_capturing && _owner.ChannelSpell == _spell && data.Damage > 0
                && data.Attacker != null && data.Attacker != _owner && data.Attacker.Team != _owner.Team
                && data.DamageResultType != DamageResultType.RESULT_DODGE
                && data.DamageResultType != DamageResultType.RESULT_MISS)
            {
                _spell.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.ChannelingCondition);
            }
        }

        public void OnUpdate(float diff)
        {
            if (!_capturing || _spell.State != SpellState.STATE_CHANNELING)
            {
                return;
            }

            _spell.ChannelCancelCheck();
            if (!_capturing || _spell.State != SpellState.STATE_CHANNELING)
            {
                return;
            }

            if (!_relic.IsCapturing(_owner, _spell))
            {
                _spell.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
                return;
            }

            float remaining = Math.Max(0, _spell.CurrentChannelDuration - diff / 1000f);
            float elapsed = ChannelDuration - remaining;
            if (elapsed < WindupDuration)
            {
                return;
            }

            if (!_engaged)
            {
                _engaged = true;
                PlayAnimation(_owner, "Channel", flags: AnimationFlags.Unknown6 | AnimationFlags.Unknown7 | AnimationFlags.Unknown8);
                _engagedBeamParticle = AddParticleTarget(
                    _owner, _owner, "OdinCaptureBeamEngaged_green.troy", _captureMinion, remaining,
                    boneNameHash: 4929107, targetBoneNameHash: 8024133,
                    enemyParticle: "OdinCaptureBeamEngaged_red.troy"
                );
                _owner.SetTargetUnit(null, true);
                _suppressionBuff = AddBuff("OdinBombSuppression", remaining, 1, _spell, _captureMinion, _owner);
            }

            _relic.SetCaptureRemaining(_spell, remaining / (ChannelDuration - WindupDuration));
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            Cleanup(true);
        }

        public void OnSpellPostChannel(Spell spell)
        {
            if (_capturing && _relic.IsCapturing(_owner, spell))
            {
                _relic.CompleteCapture(_owner, spell);
            }

            Cleanup(true);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            Cleanup(false);
            ApiEventManager.RemoveAllListenersForOwner(this);
        }

        private void Cleanup(bool applyCooldown)
        {
            if (!_capturing)
            {
                return;
            }

            _capturing = false;
            _relic.EndCapture(_spell);
            ApiEventManager.OnTakeDamage.RemoveListener(this);
            RemoveParticle(_beamParticle);
            RemoveParticle(_engagedBeamParticle);
            _beamParticle = null;
            _engagedBeamParticle = null;
            if (_captureBuff != null)
            {
                RemoveBuff(_captureBuff);
            }
            if (_suppressionBuff != null)
            {
                RemoveBuff(_suppressionBuff);
            }
            _captureBuff = null;
            _suppressionBuff = null;
            StopAnimation(_owner, "Channel", fade: true);

            if (applyCooldown && !_owner.IsDead)
            {
                AddBuff("OdinCaptureChannelCooldownBuff", 3f, 1, _spell, _owner, _owner);
            }
        }
    }
}
