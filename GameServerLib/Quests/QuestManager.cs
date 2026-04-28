using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Quests
{
    public class QuestManager
    {
        private readonly Game _game;
        private readonly List<GameQuest> _allQuests;
        private readonly List<GameQuest> _activeMatchQuests;

        public QuestManager(Game game)
        {
            _game = game;
            _allQuests = new List<GameQuest>();
            _activeMatchQuests = new List<GameQuest>();
        }

        public void Initialize()
        {
            _allQuests.Add(new TheHuntIsOnQuest(_game));
            _allQuests.Add(new ReachLevel3Quest(_game));
            _allQuests.Add(new KillZacQuest(_game));
            EvaluateQuestsForMatch();
        }

        private void EvaluateQuestsForMatch()
        {
            foreach (var quest in _allQuests)
            {
                if (quest.EvaluateMatchRequirements())
                {
                    _activeMatchQuests.Add(quest);
                    quest.OnQuestActivated += HandleQuestActivated;
                    quest.OnQuestCompleted += HandleQuestCompleted;
                    quest.RegisterActivationListeners();
                }
            }
        }

        private void HandleQuestActivated(GameQuest quest)
        {
            foreach (var player in _game.PlayerManager.GetPlayers(false))
            {
                if (player.Champion == null) continue;

                var group = quest.GetInfoQuestData(player.Champion);
                if (group != null)
                {
                    group.IsActionable = false;
                    group.SourceQuestId = quest.QuestId;
                    foreach (var uiData in group.Quests)
                    {
                        uiData.QuestId = player.Champion.PlayerQuestManager.GetNextQuestId();
                    }
                    player.Champion.PlayerQuestManager.AddQuest(group);
                }
            }
        }

        private void HandleQuestCompleted(GameQuest quest, Champion winner, Champion loser)
        {
            foreach (var player in _game.PlayerManager.GetPlayers(false))
            {
                if (player.Champion == null) continue;

                bool success = (player.Champion == winner);

                player.Champion.PlayerQuestManager.CompleteQuest(quest.QuestId, success);

                var group = quest.GetRewardOptions(player.Champion, winner, loser);
                if (group != null && group.Quests.Count > 0)
                {
                    if (group.Quests.Count == 1)
                    {
                        group.OnOptionSelected?.Invoke(group.Quests[0]);
                    }
                    else
                    {
                        group.IsActionable = true;
                        group.SourceQuestId = quest.QuestId;
                        foreach (var option in group.Quests)
                        {
                            option.QuestId = player.Champion.PlayerQuestManager.GetNextQuestId();
                        }
                        player.Champion.PlayerQuestManager.AddQuest(group);
                    }
                }
            }
        }
    }
}
