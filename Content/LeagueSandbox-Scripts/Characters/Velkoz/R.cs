using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class VelkozR : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            ChannelDuration = 2.6f,
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            AutoFaceDirection = false
        };

        private ObjAIBase _owner;
        private Minion _laserTarget;

        private Particle _eye;
        private Particle _lens;
        private Particle _beam;
        private Particle _beamEnd;
        private Particle _lensbeamC;
        private Particle _lensbeamL;
        private Particle _lensbeamR;

        private float _currentAngle;
        private float _targetAngle;
        private bool _isChanneling;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;

            _laserTarget = AddMinion(_owner, "testcuberender10vision", "testcuberender10vision", _owner.Position, _owner.Team, ignoreCollision: true, targetable: false, useSpells:false);
            _laserTarget.SetStatus(StatusFlags.NoRender, true);
            _laserTarget.SetStatus(StatusFlags.ForceRenderParticles, true);
            _laserTarget.SetCollisionRadius(-1f);
            //SetGameObjectVisibility(_laserTarget, false);
        }

        public void OnSpellChannel(Spell spell)
        {
            _isChanneling = true;

            Vector2 dir = Vector2.Normalize(new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z) - _owner.Position);
            _currentAngle = (float)Math.Atan2(dir.Y, dir.X);
            _targetAngle = _currentAngle;

            if (_laserTarget != null)
            {
                _laserTarget.SetPosition(_owner.Position + dir * 1550f, false);
            }

            AddBuff("VelkozR", 2.6f, 1, spell, _owner, _owner);

            _eye = AddParticleTarget(_owner, _owner, "velkoz_base_r_beam_eye.troy", _owner, lifetime: 2.6f, bone: "Buffbone_Cstm_EyeBall", targetBone: "Buffbone_Cstm_EyeBallTarget");
            _lens = AddParticle(_owner, _owner, "velkoz_base_r_lens.troy", _owner.Position, lifetime: 2.6f, bone: "Buffbone_Cstm_EyeBallTarget");

            _beam = AddParticleTarget(_owner, _owner, "velkoz_base_r_beam.troy", _laserTarget, lifetime: 2.6f, bone: "Buffbone_Cstm_EyeBallTarget");
            _beamEnd = AddParticleTarget(_owner, _laserTarget, "velkoz_base_r_beam_end.troy", _laserTarget, lifetime: 2.6f);

            _lensbeamC = AddParticleTarget(_owner, _owner, "velkoz_base_r_lensbeam.troy", _owner, lifetime: 2.6f, bone: "C_Buffbone_Cstm_Tenticle", targetBone: "Buffbone_Cstm_EyeBallTarget");
            _lensbeamL = AddParticleTarget(_owner, _owner, "velkoz_base_r_lensbeam.troy", _owner, lifetime: 2.6f, bone: "L_Buffbone_Cstm_Tenticle", targetBone: "Buffbone_Cstm_EyeBallTarget");
            _lensbeamR = AddParticleTarget(_owner, _owner, "velkoz_base_r_lensbeam.troy", _owner, lifetime: 2.6f, bone: "R_Buffbone_Cstm_Tenticle", targetBone: "Buffbone_Cstm_EyeBallTarget");
        }

        public void OnSpellChannelUpdate(Spell spell, Vector3 position, bool forceStop)
        {
            Vector2 targetDir = Vector2.Normalize(new Vector2(position.X, position.Z) - _owner.Position);
            _targetAngle = (float)Math.Atan2(targetDir.Y, targetDir.X);
        }

        public void OnUpdate(float diff)
        {
            if (_isChanneling && _laserTarget != null)
            {
                float angleDiff = _targetAngle - _currentAngle;

                while (angleDiff > Math.PI) angleDiff -= (float)(2 * Math.PI);
                while (angleDiff < -Math.PI) angleDiff += (float)(2 * Math.PI);

                if (Math.Abs(angleDiff) > 0.001f)
                {
                    float turnRate = 0.8f;
                    float step = turnRate * (diff / 1000f);

                    if (Math.Abs(angleDiff) <= step)
                    {
                        _currentAngle = _targetAngle;
                    }
                    else
                    {
                        _currentAngle += Math.Sign(angleDiff) * step;
                    }

                    while (_currentAngle > Math.PI) _currentAngle -= (float)(2 * Math.PI);
                    while (_currentAngle < -Math.PI) _currentAngle += (float)(2 * Math.PI);

                    Vector2 newDir = new Vector2((float)Math.Cos(_currentAngle), (float)Math.Sin(_currentAngle));
                    Vector2 newPos = _owner.Position + newDir * 1550f;

                    _laserTarget.DashToLocation(newPos, 1240f, "", 0f, false, false, "", null, true);
                    _owner.FaceDirection(new Vector3(newDir.X, 0, newDir.Y), true);
                }
            }
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            _owner.RemoveBuffsWithName("VelkozR");
            _isChanneling = false;
            RemoveParticles();
        }

        public void OnSpellPostChannel(Spell spell)
        {
            _isChanneling = false;
            RemoveParticles();
        }

        private void RemoveParticles()
        {
            RemoveParticle(_eye);
            RemoveParticle(_lens);
            RemoveParticle(_beam);
            RemoveParticle(_beamEnd);
            RemoveParticle(_lensbeamC);
            RemoveParticle(_lensbeamL);
            RemoveParticle(_lensbeamR);
        }
    }
}