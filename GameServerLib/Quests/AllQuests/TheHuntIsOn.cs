using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Chatbox;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Quests
{
    public class TheHuntIsOnQuest : GameQuest
    {
        private Champion _rengar;
        private Champion _khazix;

        public TheHuntIsOnQuest(Game game) : base(game, "TheHuntIsOn") { }

        public override bool EvaluateMatchRequirements()
        {
            foreach (var player in _game.PlayerManager.GetPlayers(false))
            {
                if (player.Champion.Model == "Rengar") _rengar = player.Champion;
                if (player.Champion.Model == "Khazix") _khazix = player.Champion;
            }

            return _rengar != null && _khazix != null && _rengar.Team != _khazix.Team;
        }

        public override void RegisterActivationListeners()
        {
            ApiEventManager.OnLevelUp.AddListener(this, _rengar, CheckActivation, false);
            ApiEventManager.OnLevelUp.AddListener(this, _khazix, CheckActivation, false);
        }

        private void CheckActivation(AttackableUnit unit)
        {
            if (_rengar.Stats.Level >= 16 && _khazix.Stats.Level >= 16)
            {
                Activate();
            }
        }

        public override QuestDisplayGroup GetInfoQuestData(Champion player)
        {
            if (player == _rengar)
            {
                var group = new QuestDisplayGroup();
                group.AddQuest(new UIQuestData {
                    Objective = "game_quest_name_khazixhunt", 
                    QuestType = QuestType.Secondary, 
                    IsTip = false, 
                    Tooltip = "game_quest_description_rengarhunt" });
                return group;
            }
            if (player == _khazix)
            {
                var group = new QuestDisplayGroup();
                group.AddQuest(new UIQuestData { 
                    Objective = "game_quest_name_khazixhunt", 
                    QuestType = QuestType.Secondary, 
                    IsTip = false, 
                    Tooltip = "game_quest_description_khazixhunt" });
                return group;
            }
            return null;
        }

        protected override void RegisterCompletionListeners()
        {
            ApiEventManager.OnDeath.AddListener(this, _rengar, OnChampionDeath, false);
            ApiEventManager.OnDeath.AddListener(this, _khazix, OnChampionDeath, false);
        }

        private void OnChampionDeath(DeathData deathData)
        {
            if (deathData.Unit == _khazix && (deathData.Killer == _rengar))
            {
                Complete(_rengar, _khazix);
            }
            else if (deathData.Unit == _rengar && (deathData.Killer == _khazix))
            {
                Complete(_khazix, _rengar);
            }
        }

        public override QuestDisplayGroup GetRewardOptions(Champion player, Champion winner, Champion loser)
        {
            if (player != winner) return null;

            var group = new QuestDisplayGroup();

            if (winner == _rengar)
            {
                group.AddQuest(new UIQuestData
                {
                    InternalName = "head_of_khazix",
                    Objective = "game_quest_reward_head_of_khazix",
                    IsTip = true 
                });
            }
            else if (winner == _khazix)
            {
                group.AddQuest(new UIQuestData
                {
                    InternalName = "evolve_4th",
                    Objective = "game_quest_reward_evolve_4th",
                    IsTip = true 
                });
            }

            group.OnOptionSelected = (clickedData) =>
            {
                if (clickedData.InternalName == "head_of_khazix")
                {
                    _game.ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, "Rengar claimed the Head of Kha'Zix!");
                }
                else if (clickedData.InternalName == "evolve_4th")
                {
                    _game.ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, "Kha'Zix claimed his 4th evolution!");
                }
            };

            return group;
        }

        public override void UnregisterAllListeners()
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
