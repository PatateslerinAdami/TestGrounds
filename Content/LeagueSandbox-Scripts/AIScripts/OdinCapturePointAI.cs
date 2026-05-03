using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace AIScripts
{
    public class OdinCapturePointAI : IAIScript, IOdinCapturePoint
    {
        public char PointLetter { get; set; } = 'A';
        public byte PointIndex { get; set; } = 0;
        private Minion _self;
        private List<Champion> _capturers = new List<Champion>();

        private const float MAX_MANA = 50000f;
        private const float HALF_MANA = 25000f;
        private const float MIN_MANA = 0f;

        private const float BASE_CAPTURE_RATE = 6250f;
        private const float ATTACK_RANGE = 800F;
        private const float ATTACK_COOLDOWN_TIME = 2.0f;

        private enum CaptureState
        {
            Idle,
            Filling,
            Emptying
        }

        private CaptureState _currentVisualState = CaptureState.Idle;
        private Particle _progressParticle;
        private Particle _guardianParticle;
        private Buff _soundBuff;
        private Buff _stunBuff;

        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData();
        private float _attackCooldown = 0f;
        private float _manaShiftPauseTimer = 0f;

        public void OnActivate(ObjAIBase owner)
        {
            _self = owner as Minion;
            _self.SetStatus(StatusFlags.CanAttack, false);

            AddBuff("OdinGuardianBuff", 25000f, 1, _self.Spells.Values.FirstOrDefault(), _self, _self);
        }

        public void AddCapturer(Champion champion)
        {
            if (!_capturers.Contains(champion)) _capturers.Add(champion);
        }

        public void RemoveCapturer(Champion champion)
        {
            _capturers.Remove(champion);
        }

        public void OnUpdate(float diff)
        {
            if (_manaShiftPauseTimer > 0)
            {
                _manaShiftPauseTimer -= diff / 1000f;
            }
            _capturers.RemoveAll(c => c == null || c.IsDead || c.ChannelSpell == null);

            if (_capturers.Count > 0 && _stunBuff == null)
            {
                _stunBuff = AddBuff("Stun", 25000f, 1, _self.Spells.Values.FirstOrDefault(), _self, _self);
            }
            else if (_capturers.Count == 0 && _stunBuff != null)
            {
                RemoveBuff(_stunBuff);
                _stunBuff = null;
                SetVisualState(CaptureState.Idle, null);
            }

            int bluePower = _capturers.Count(c => c.Team == TeamId.TEAM_BLUE);
            int purplePower = _capturers.Count(c => c.Team == TeamId.TEAM_PURPLE);

            if (_self.Team == TeamId.TEAM_NEUTRAL)
            {
                HandleNeutralState(bluePower, purplePower, diff);

                if (_self.TargetUnit != null)
                {
                    _self.SetTargetUnit(null);
                }
            }
            else
            {
                HandleOwnedState(bluePower, purplePower, diff);

                if (_self.Team != TeamId.TEAM_NEUTRAL)
                {
                    if (_attackCooldown > 0)
                    {
                        _attackCooldown -= diff / 1000f;
                    }

                    if (!_self.IsAttacking)
                    {
                        CheckForTargets();
                    }

                    if (_self.TargetUnit != null)
                    {
                        if (Vector2.DistanceSquared(_self.Position, _self.TargetUnit.Position) > ATTACK_RANGE * ATTACK_RANGE)
                        {
                            _self.SetTargetUnit(null, true);
                        }
                        else if (_attackCooldown <= 0)
                        {
                            _attackCooldown = ATTACK_COOLDOWN_TIME;

                            var target = _self.TargetUnit;

                            _self.GetSpell("OdinGuardianSpellAttackCast").Cast(_self.Position, target.Position, target);
                            _self.GetSpell("OdinGuardianSpellAttack").Cast(_self.Position, target.Position, target);
                        }
                    }
                }
            }
        }


        private void CheckForTargets()
        {
            var units = GetUnitsInRange(
                _self,
                _self.Position,
                ATTACK_RANGE,
                true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes
            );
            AttackableUnit nextTarget = null;
            var nextTargetPriority = ClassifyUnit.DEFAULT;

            foreach (var u in units)
            {
                if (u.IsDead || u.Team == _self.Team || !u.Status.HasFlag(StatusFlags.Targetable))
                {
                    continue;
                }

                if (u is ObjAIBase ai && ai.ChannelSpell != null && ai.ChannelSpell.SpellName == "OdinCaptureChannel")
                {
                    if(_self.TargetUnit  != null && _self.TargetUnit == ai)
                    {
                       _self.SetTargetUnit(null, true); 
                    }
                    continue;
                }

                if (_self.TargetUnit == null)
                {
                    var priority = _self.ClassifyTarget(u);
                    if (priority < nextTargetPriority)
                    {
                        nextTarget = u;
                        nextTargetPriority = priority;
                    }
                }
                else
                {
                    if (_self.TargetUnit is Champion)
                    {
                        continue;
                    }

                    if (!(u is Champion enemyChamp) || enemyChamp.TargetUnit == null)
                    {
                        continue;
                    }

                    if (!(enemyChamp.TargetUnit is Champion enemyChampTarget) ||
                        Vector2.DistanceSquared(enemyChamp.Position, enemyChampTarget.Position) >
                        enemyChamp.Stats.Range.Total * enemyChamp.Stats.Range.Total ||
                        Vector2.DistanceSquared(_self.Position, enemyChampTarget.Position) >
                        ATTACK_RANGE * ATTACK_RANGE)
                    {
                        continue;
                    }

                    nextTarget = enemyChamp;
                    break;
                }
            }

            if (nextTarget != null)
            {
                _self.SetTargetUnit(nextTarget, true);
            }
        }

        private void SetVisualState(CaptureState newState, Champion dominantCapturer)
        {
            if (_currentVisualState == newState) return;
            _currentVisualState = newState;

            _progressParticle?.SetToRemove();
            if (_soundBuff != null)
            {
                RemoveBuff(_soundBuff);
                _soundBuff = null;
            }

            if (newState == CaptureState.Filling)
            {
                _soundBuff = AddBuff("OdinCaptureSoundFilling", 25000f, 1, _self.Spells.Values.FirstOrDefault(), _self, _self);
                if (dominantCapturer != null)
                {
                    _progressParticle = AddParticleTarget(dominantCapturer, null, "Odin-Capture-Filling.troy", _self, 25000f);
                }
            }
            else if (newState == CaptureState.Emptying)
            {
                _soundBuff = AddBuff("OdinCaptureSoundEmptying", 25000f, 1, _self.Spells.Values.FirstOrDefault(), _self, _self);
                if (dominantCapturer != null)
                {
                    _progressParticle = AddParticleTarget(dominantCapturer, null, "Odin-Capture-Emptying.troy", _self, 25000f);
                }
            }
        }

        private void HandleNeutralState(int bluePower, int purplePower, float diff)
        {
            int netPower = bluePower - purplePower;

            if (netPower == 0)
            {
                SetVisualState(CaptureState.Idle, null);
                return;
            }

            TeamId dominantTeam = netPower > 0 ? TeamId.TEAM_BLUE : TeamId.TEAM_PURPLE;
            Champion dominantCapturer = _capturers.FirstOrDefault(c => c.Team == dominantTeam);

            SetVisualState(CaptureState.Filling, dominantCapturer);

            if (_manaShiftPauseTimer > 0)
            {
                return;
            }

            float manaShift = BASE_CAPTURE_RATE * netPower * (diff / 1000f);
            _self.Stats.CurrentMana += manaShift;

            if (_self.Stats.CurrentMana >= MAX_MANA)
            {
                CapturePoint(TeamId.TEAM_BLUE);
            }
            else if (_self.Stats.CurrentMana <= MIN_MANA)
            {
                CapturePoint(TeamId.TEAM_PURPLE);
            }
        }

        private void HandleOwnedState(int bluePower, int purplePower, float diff)
        {
            int ownerPower = _self.Team == TeamId.TEAM_BLUE ? bluePower : purplePower;
            int enemyPower = _self.Team == TeamId.TEAM_BLUE ? purplePower : bluePower;

            int netOwnerPower = ownerPower - enemyPower;

            if (netOwnerPower == 0)
            {
                SetVisualState(CaptureState.Idle, null);
                return;
            }
            float manaShift = BASE_CAPTURE_RATE * netOwnerPower * (diff / 1000f);
            if (netOwnerPower < 0)
            {
                TeamId enemyTeam = _self.Team == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE;
                Champion dominantCapturer = _capturers.FirstOrDefault(c => c.Team == enemyTeam);
                SetVisualState(CaptureState.Emptying, dominantCapturer);
                manaShift *= 2.0f;
            }
            else
            {
                Champion dominantCapturer = _capturers.FirstOrDefault(c => c.Team == _self.Team);
                SetVisualState(CaptureState.Filling, dominantCapturer);
            }

            _self.Stats.CurrentMana += manaShift;

            if (_self.Stats.CurrentMana > MAX_MANA)
            {
                _self.Stats.CurrentMana = MAX_MANA;
            }
            else if (_self.Stats.CurrentMana <= MIN_MANA)
            {
                NeutralizePoint();
            }
        }

        private void CapturePoint(TeamId newTeam)
        {
            _self.SetTeam(newTeam);
            _self.Stats.CurrentMana = MAX_MANA;

            PlayAnimation(_self, "Activate", scaleTime: 0, startProgress: 0, speedRatio: 1, flags: AnimationFlags.Unknown6 | AnimationFlags.Unknown7 | AnimationFlags.Unknown8);
            OverrideAnimation(_self, "FLOATING", "IDLE1");

            _guardianParticle?.SetToRemove();


            _guardianParticle = AddParticleTarget(_self, _self, "OdinNeutralGuardian_Green.troy", _self, 25000f, enemyParticle: "OdinNeutralGuardian_Red.troy", boneNameHash: 178301468);

            var capturer = _capturers.Find(c => c.Team == newTeam);
            ApiGameEvents.AnnounceCapturePointCaptured(_self, GetPointLetter(), capturer);

            InterruptAllCapturers();
        }

        private void NeutralizePoint()
        {
            _self.SetTeam(TeamId.TEAM_NEUTRAL);
            _self.Stats.CurrentMana = HALF_MANA;
            _self.Replication.Update();

            _self.SetTargetUnit(null, true);

            _manaShiftPauseTimer = 0.3f;

            PlayAnimation(_self, "Deactivate", scaleTime: 0, startProgress: 0, speedRatio: 1, flags: AnimationFlags.Override | AnimationFlags.Unknown6 | AnimationFlags.Unknown7 | AnimationFlags.Unknown8);
            ClearOverrideAnimation(_self, "IDLE1");

            _guardianParticle?.SetToRemove();
            _guardianParticle = null;

            ApiGameEvents.AnnounceCapturePointNeutralized(_self, GetPointLetter());
        }

        private void InterruptAllCapturers()
        {
            foreach (var c in _capturers.ToArray())
            {
                c.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.TimeCompleted);
            }
            _capturers.Clear();
        }

        private char GetPointLetter()
        {
            return PointLetter;
        }

        private byte GetPointIndex()
        {
            return PointIndex;
        }
    }
}