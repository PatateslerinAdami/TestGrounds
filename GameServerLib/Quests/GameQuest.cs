using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Quests
{
    public abstract class GameQuest
    {
        public string QuestId { get; }
        public QuestState State { get; protected set; }

        public event Action<GameQuest> OnQuestActivated;
        public event Action<GameQuest, Champion, Champion> OnQuestCompleted;

        protected Game _game;

        protected GameQuest(Game game, string questId)
        {
            _game = game;
            QuestId = questId;
            State = QuestState.Ineligible;
        }

        public abstract bool EvaluateMatchRequirements();
        public abstract void RegisterActivationListeners();

        protected virtual void Activate()
        {
            if (State == QuestState.Active) return;

            State = QuestState.Active;
            OnQuestActivated?.Invoke(this);

            RegisterCompletionListeners();
        }

        protected abstract void RegisterCompletionListeners();

        protected virtual void Complete(Champion winner, Champion loser)
        {
            State = QuestState.Completed;
            OnQuestCompleted?.Invoke(this, winner, loser);
            UnregisterAllListeners();
        }

        public abstract void UnregisterAllListeners();

        public abstract QuestDisplayGroup GetInfoQuestData(Champion player);
        public abstract QuestDisplayGroup GetRewardOptions(Champion player, Champion winner, Champion loser);
    }
}
