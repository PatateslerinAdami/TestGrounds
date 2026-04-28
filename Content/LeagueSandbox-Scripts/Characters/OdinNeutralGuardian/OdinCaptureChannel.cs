using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells
{
    public class OdinCaptureChannel : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            ChannelDuration = 30f,
        };

        private Particle _beamParticle;
        private Champion _owner;
        private IOdinCapturePoint _capturePoint;

        public void OnSpellCast(Spell spell)
        {
            _owner = spell.CastInfo.Owner as Champion;
            var targetMinion = spell.CastInfo.Targets[0].Unit as Minion;

            _capturePoint = targetMinion?.AIScript as IOdinCapturePoint;
        }

        public void OnSpellChannel(Spell spell)
        {
            _capturePoint?.AddCapturer(_owner);
            // The beam particle for later
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            Cleanup();
        }

        public void OnSpellPostChannel(Spell spell)
        {
            Cleanup();
        }

        private void Cleanup()
        {
            _capturePoint?.RemoveCapturer(_owner);
            _beamParticle?.SetToRemove();
        }
    }
}