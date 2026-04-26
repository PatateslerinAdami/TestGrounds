using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class VarusEZoneTracker : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
            MaxStacks = 100,
            IsHidden = false,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private ObjAIBase _owner;
        private Spell _spell;
        private Vector2 _targetPos;

        private bool _wasDestroyed = false;
        private bool _zoneActive = false;
        private float _tickTimer = 0f;

        private List<SpellMissile> _visualMissiles = new List<SpellMissile>();
        private Particle _zoneParticle;
        private Particle _zoneParticleGreen;
        private Particle _zoneParticleRed;
        private Particle _zoneSound;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = ownerSpell.CastInfo.Owner;
            _spell = ownerSpell;
            _targetPos = new Vector2(buff.Variables.GetFloat("TargetX"), buff.Variables.GetFloat("TargetY"));

            var mainMissile = CreateCustomMissile(_owner, "VarusEMissile", _owner.Position, _targetPos,
                new MissileParameters { Type = MissileType.Arc });
            if (mainMissile != null)
            {
                ApiEventManager.OnSpellMissileEnd.AddListener(this, mainMissile, OnMainMissileEnd, true);
            }

            Random rnd = new Random();
            int visualMissileCount = 9;
            for (int i = 0; i < visualMissileCount; i++)
            {
                float angle = (float)(rnd.NextDouble() * Math.PI * 2);
                float radius = (float)(rnd.NextDouble() * 250f);
                Vector2 offset = new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius);
                Vector2 visualEndPos = _targetPos + offset;
                //At some point should look over hit registering of missile dummies since they also calls OnBeingSpellHit
                var visMissile = CreateCustomMissile(_owner, "VarusEMissileDummy", _owner.Position, visualEndPos,
                    new MissileParameters { Type = MissileType.Arc });
                if (visMissile != null)
                {
                    _visualMissiles.Add(visMissile);
                }
            }

            _owner.RegisterTimer(new GameScriptTimer(0.5f, () =>
            {
                if (_wasDestroyed)
                {
                    buff.DeactivateBuff();
                    return;
                }

                _zoneActive = true;

                var units = GetUnitsInRange(_owner, _targetPos, 300f, true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                    SpellDataFlags.AffectNeutral);
                foreach (var target in units)
                {
                    _spell.ApplyEffects(target);
                    AddParticleTarget(_owner, target, "globalhit_orange_tar.troy", target, 1.0f);
                }

                _zoneParticle = AddParticlePos(_owner, "varuseaoe.troy", _targetPos, _targetPos, 4.0f);
                _zoneSound = AddParticlePos(_owner, "varusehit_sound.troy", _targetPos, _targetPos, 4.0f);

                _zoneParticleGreen = AddParticlePos(_owner, "varusecirclegreen.troy", _targetPos, _targetPos, 4.0f,
                    teamOnly: _owner.Team);
                _zoneParticleRed = AddParticlePos(_owner, "varusecirclered.troy", _targetPos, _targetPos, 4.0f,
                    teamOnly: CustomConvert.GetEnemyTeam(_owner.Team));
            }));
        }

        public void OnMainMissileEnd(SpellMissile missile)
        {
            if (Vector2.Distance(missile.Position, _targetPos) > 50f)
            {
                _wasDestroyed = true;

                foreach (var visMissile in _visualMissiles)
                {
                    if (visMissile != null && !visMissile.IsToRemove())
                    {
                        visMissile.SetToRemove();
                    }
                }
            }
            else
            {
                AddParticlePos(_owner, "VarusEAOEMini.troy", missile.Position, missile.Position, 5.0f);

                foreach (var visMissile in _visualMissiles)
                {
                    if (visMissile != null && !visMissile.IsToRemove())
                    {
                        Vector2 visTargetPos = new Vector2(visMissile.CastInfo.TargetPositionEnd.X,
                            visMissile.CastInfo.TargetPositionEnd.Z);
                        AddParticlePos(_owner, "VarusEAOEMini.troy", visTargetPos, visTargetPos, 5.0f);
                        visMissile.SetToRemove();
                    }
                }
            }

            _visualMissiles.Clear();
        }

        public void OnUpdate(float diff)
        {
            if (!_zoneActive) return;

            _tickTimer += diff;
            if (_tickTimer >= 250f)
            {
                _tickTimer = 0f;
                var units = GetUnitsInRange(_owner, _targetPos, 300f, true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                    SpellDataFlags.AffectNeutral);
                foreach (var target in units)
                {
                    AddBuff("VarusESlow", 0.25f, 1, _spell, target, _owner, false);
                }
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_zoneParticle != null && !_zoneParticle.IsToRemove())
            {
                _zoneParticle.SetToRemove();
            }

            if (_zoneParticleGreen != null && !_zoneParticleGreen.IsToRemove())
            {
                _zoneParticleGreen.SetToRemove();
            }

            if (_zoneParticleRed != null && !_zoneParticleRed.IsToRemove())
            {
                _zoneParticleRed.SetToRemove();
            }

            if (_zoneSound != null && !_zoneSound.IsToRemove())
            {
                _zoneSound.SetToRemove();
            }
        }
    }

    public class VarusESlow : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private Particle _gwParticle;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            float[] slowAmounts = { 0.25f, 0.30f, 0.35f, 0.40f, 0.45f };
            int level = ownerSpell.CastInfo.SpellLevel - 1;
            if (level < 0) level = 0;
            if (level > 4) level = 4;

            StatsModifier.MoveSpeed.PercentBonus = -slowAmounts[level];
            unit.AddStatModifier(StatsModifier);

            _gwParticle = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "global_mortal_strike.troy", unit, 0.5f);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_gwParticle != null && !_gwParticle.IsToRemove())
            {
                _gwParticle.SetToRemove();
            }
        }
    }
}