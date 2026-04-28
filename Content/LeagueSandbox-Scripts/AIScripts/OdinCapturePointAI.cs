using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData();

        public void OnActivate(ObjAIBase owner)
        {
            _self = owner as Minion;
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
            _capturers.RemoveAll(c => c == null || c.IsDead || c.ChannelSpell == null);

            int bluePower = _capturers.Count(c => c.Team == TeamId.TEAM_BLUE);
            int purplePower = _capturers.Count(c => c.Team == TeamId.TEAM_PURPLE);

            if (_self.Team == TeamId.TEAM_NEUTRAL)
            {
                HandleNeutralState(bluePower, purplePower, diff);
            }
            else
            {
                HandleOwnedState(bluePower, purplePower, diff);
            }
        }

        private void HandleNeutralState(int bluePower, int purplePower, float diff)
        {
            int netPower = bluePower - purplePower;

            if (netPower == 0) return;

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

            if (netOwnerPower == 0) return;

            float manaShift = BASE_CAPTURE_RATE * netOwnerPower * (diff / 1000f);
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

            var capturer = _capturers.Find(c => c.Team == newTeam);
            ApiGameEvents.AnnounceCapturePointCaptured(_self, GetPointLetter(), capturer);

            InterruptAllCapturers();
        }

        private void NeutralizePoint()
        {
            _self.SetTeam(TeamId.TEAM_NEUTRAL);
            _self.Stats.CurrentMana = HALF_MANA;

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