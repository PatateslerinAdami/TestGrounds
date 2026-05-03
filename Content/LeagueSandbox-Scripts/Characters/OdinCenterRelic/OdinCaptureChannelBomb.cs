using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class OdinCaptureChannelBomb : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            ChannelDuration = 4.50f, 
        };

        private Particle _beamParticle;
        private Particle _engagedBeamParticle;
        private Champion _owner;
        private IOdinCapturePoint _capturePoint;
        private Minion _captureMinion;
        private Buff _captureBuff;

        public void OnSpellCast(Spell spell)
        {
            _owner = spell.CastInfo.Owner as Champion;
            _captureMinion = spell.CastInfo.Targets[0].Unit as Minion;
            //_capturePoint = _captureMinion?.AIScript as IOdinCapturePoint;

            _beamParticle = AddParticleTarget(_owner, _owner, "OdinCaptureBeam.troy", _captureMinion, 25000f, boneNameHash: 4929107, targetBoneNameHash: 8024133);
            _captureBuff = AddBuff("OdinCaptureChannel", 30f, 1, spell, _owner, _owner);

        }

        public void OnSpellChannel(Spell spell)
        {
            CreateTimer(1.25f, () =>
            {
                if (_owner.ChannelSpell != spell) return;

                //_capturePoint?.AddCapturer(_owner);
                PlayAnimation(_owner, "Channel", flags: AnimationFlags.Unknown6 | AnimationFlags.Unknown7 | AnimationFlags.Unknown8);

                _engagedBeamParticle = AddParticleTarget(_owner, _owner, "OdinCaptureBeamEngaged_green.troy", _captureMinion, 25000f, boneNameHash: 4929107, targetBoneNameHash: 8024133, enemyParticle: "OdinCaptureBeamEngaged_red.troy");
                _owner.SetTargetUnit(null, true);
                AddBuff("OdinBombSupression", 10f, 1, spell, _captureMinion, _owner);
            });
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            Cleanup(spell);
        }

        public void OnSpellPostChannel(Spell spell)
        {
            Cleanup(spell);
        }

        private void Cleanup(Spell spell)
        {
            //_capturePoint?.RemoveCapturer(_owner);
            _beamParticle?.SetToRemove();
            _engagedBeamParticle?.SetToRemove();
            _captureBuff?.DeactivateBuff();
            StopAnimation(_owner, "Channel", fade: true);

            AddBuff("OdinCaptureChannelCooldownBuff", 3f, 1, spell, _owner, _owner);
        }
    }
}