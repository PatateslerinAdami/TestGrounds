using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.Logging;
using System.Linq;

namespace LeagueSandbox.GameServer.Quests
{
    public class OdinQuest : GameQuest
    {
        private bool _isQuestActive = false;
        private float _nextQuestTime = 0f;

        private const float QUEST_INTERVAL_SECONDS = 300.0f;
        private const float QUEST_BUFF_DURATION_SECONDS = 50.0f;

        private Minion _targetForBlueToCapture;
        private Minion _targetForPurpleToCapture;
        private Minion _blueQuestIndicator;
        private Minion _purpleQuestIndicator;
        private Particle _blueQuestParticle;
        private Particle _purpleQuestParticle;

        public OdinQuest(Game game) : base(game, "OdinQuest")
        {
            IsTeamQuest = true;
        }

        public override bool EvaluateMatchRequirements()
        {
            return _game.Map.Id == 8;
        }

        public override void RegisterActivationListeners()
        {
            LoggerProvider.GetLogger().Info("[OdinQuest] Registered. Waiting for game to start...");

            _nextQuestTime = QUEST_INTERVAL_SECONDS;

            _game.AddGameScriptTimer(new GameScriptTimer(5.0f, CheckQuestTimer));
        }

        private void CheckQuestTimer()
        {
            if (_isQuestActive)
            {
                _game.AddGameScriptTimer(new GameScriptTimer(5.0f, CheckQuestTimer));
                return;
            }

            float currentGameTimeSeconds = _game.GameTime / 1000f;


            if (currentGameTimeSeconds >= _nextQuestTime)
            {
                TryActivateQuest();
            }

            _game.AddGameScriptTimer(new GameScriptTimer(5.0f, CheckQuestTimer));
        }

        private void TryActivateQuest()
        {

            var capturePoints = _game.ObjectManager.GetObjects().Values
                .OfType<Minion>()
                .Where(m => m.Name == "OdinNeutralGuardian")
                .ToList();

            var bluePoints = capturePoints.Where(p => p.Team == TeamId.TEAM_BLUE).ToList();
            var purplePoints = capturePoints.Where(p => p.Team == TeamId.TEAM_PURPLE).ToList();

            if (bluePoints.Count == 0 || purplePoints.Count == 0)
            {
                _nextQuestTime = (_game.GameTime / 1000f) + 10.0f;
                return;
            }

            _isQuestActive = true;

            var rand = new System.Random();
            _targetForBlueToCapture = purplePoints[rand.Next(purplePoints.Count)];
            _targetForPurpleToCapture = bluePoints[rand.Next(bluePoints.Count)];

            var announcerUnit = _game.ObjectManager.GetObjects().Values
                .OfType<LaneTurret>()
                .FirstOrDefault(t => t.Team == TeamId.TEAM_BLUE);

            if (announcerUnit != null)
            {
                ApiGameEvents.AnnounceVictoryPointThreshold(announcerUnit, 4);
            }

            _blueQuestIndicator = CreateQuestIndicator(_targetForBlueToCapture, TeamId.TEAM_BLUE, "Quest");
            _purpleQuestIndicator = CreateQuestIndicator(_targetForPurpleToCapture, TeamId.TEAM_PURPLE, "Quest_Chaos");
            _blueQuestParticle = CreateQuestParticle(_targetForBlueToCapture);
            _purpleQuestParticle = CreateQuestParticle(_targetForPurpleToCapture);

            Activate();
        }

        private Minion CreateQuestIndicator(Minion point, TeamId attackingTeam, string iconCategory)
        {
            var indicator = new Minion(_game, null, point.Position, "OdinQuestIndicator", "OdinQuestIndicator",
                team: attackingTeam, ignoreCollision: true, targetable: false, enableScripts: false)
            {
                DisableFoW = true
            };
            indicator.SetStatus(StatusFlags.CanMoveEver, false);
            indicator.SetStatus(StatusFlags.CanAttack, false);
            indicator.SetStatus(StatusFlags.Invulnerable, true);

            indicator.Stats.Size.BaseValue = 0;
            indicator.Stats.PerceptionRange.BaseValue = 0;
            indicator.IconInfo.ChangeIcon(iconCategory);
            _game.ObjectManager.AddObject(indicator);
            return indicator;
        }

        private Particle CreateQuestParticle(Minion point)
        {
            return new Particle(_game, point, point, point, "odin_point_active.troy",
                reqVision: false, lifetime: -1.0f, flags: FXFlags.BindDirection);
        }

        private void ClearQuestVisuals()
        {
            _blueQuestParticle?.SetToRemove();
            _purpleQuestParticle?.SetToRemove();
            _blueQuestParticle = null;
            _purpleQuestParticle = null;

            RemoveQuestIndicator(_blueQuestIndicator);
            RemoveQuestIndicator(_purpleQuestIndicator);
            _blueQuestIndicator = null;
            _purpleQuestIndicator = null;
        }

        private void RemoveQuestIndicator(Minion indicator)
        {
            if (indicator == null || indicator.IsToRemove()) return;

            indicator.IconInfo.ResetIcon();
            foreach (var player in _game.PlayerManager.GetPlayers(false))
            {
                if (indicator.IsSpawnedForPlayer(player.ClientId))
                {
                    _game.PacketNotifier.NotifyS2C_UnitSetMinimapIcon(player.ClientId, indicator, true, false);
                }
            }

            indicator.Stats.CurrentHealth = 0;
            _game.PacketNotifier.NotifyS2C_NPC_Die_MapView(new DeathData { Unit = indicator, Killer = indicator });
            indicator.SetToRemove();
        }

        private char GetPointLetter(Minion point)
        {
            if (point.AIScript != null)
            {
                var prop = point.AIScript.GetType().GetProperty("PointLetter");
                if (prop != null)
                {
                    return (char)prop.GetValue(point.AIScript);
                }
            }
            return 'A';
        }

        public override QuestDisplayGroup GetInfoQuestData(Champion player)
        {
            var group = new QuestDisplayGroup();

            char pointToCapture = player.Team == TeamId.TEAM_BLUE ? GetPointLetter(_targetForBlueToCapture) : GetPointLetter(_targetForPurpleToCapture);
            char pointToDefend = player.Team == TeamId.TEAM_BLUE ? GetPointLetter(_targetForPurpleToCapture) : GetPointLetter(_targetForBlueToCapture);

            group.AddQuest(new UIQuestData
            {
                Objective = $"game_quest_capture_{char.ToLower(pointToCapture)}",
                QuestType = QuestType.Primary,
                IsTip = false
                //Tooltip = $"game_quest_capture_{char.ToLower(pointToCapture)}"
            });

            group.AddQuest(new UIQuestData
            {
                Objective = $"game_quest_defend_{char.ToLower(pointToDefend)}",
                QuestType = QuestType.Primary,
                IsTip = false
            });
            //Tooltip = $"game_quest_defend_{char.ToLower(pointToDefend)}"
            return group;
        }

        protected override void RegisterCompletionListeners()
        {
            ApiEventManager.OnCapturePoint.AddListener(this, _targetForBlueToCapture, OnPointCaptured, false);
            ApiEventManager.OnCapturePoint.AddListener(this, _targetForPurpleToCapture, OnPointCaptured, false);
        }

        private void OnPointCaptured(AttackableUnit point, Champion capturer)
        {
            if (!_isQuestActive || capturer == null) return;

            if (point == _targetForBlueToCapture && capturer.Team == TeamId.TEAM_BLUE)
            {
                LoggerProvider.GetLogger().Info("[OdinQuest] Blue team completed the quest!");
                CompleteOdinQuest(TeamId.TEAM_BLUE);
            }
            else if (point == _targetForPurpleToCapture && capturer.Team == TeamId.TEAM_PURPLE)
            {
                LoggerProvider.GetLogger().Info("[OdinQuest] Purple team completed the quest!");
                CompleteOdinQuest(TeamId.TEAM_PURPLE);
            }
        }

        public void CompleteOdinQuest(TeamId winningTeam)
        {
            if (!_isQuestActive) return;
            _isQuestActive = false;
            ClearQuestVisuals();

            TeamId losingTeam = winningTeam == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE;

            LoggerProvider.GetLogger().Info($"[OdinQuest] Quest over. Winning Team: {winningTeam}. Draining 20 points from {losingTeam}.");

            ApiEventManager.OnQuestComplete.Publish(losingTeam, 20);
            Champion winningChamp = null;
            foreach (var player in _game.PlayerManager.GetPlayers())
            {
                var champion = player.Champion;
                if (champion != null && champion.Team == winningTeam)
                {
                    winningChamp = champion;
                    if (!champion.IsDead)
                    {
                        ApiFunctionManager.AddBuff("OdinQuestBuff", QUEST_BUFF_DURATION_SECONDS, 1, null, champion, champion);
                    }
                }
            }

            _nextQuestTime = (_game.GameTime / 1000f) + QUEST_INTERVAL_SECONDS;
            LoggerProvider.GetLogger().Info($"[OdinQuest] Queuing next quest for GameTime: {_nextQuestTime}s.");

            Complete(winningChamp, null);
        }

        public override QuestDisplayGroup GetRewardOptions(Champion player, Champion winner, Champion loser)
        {
            return null;
        }

        public override void UnregisterAllListeners()
        {
            ClearQuestVisuals();
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}