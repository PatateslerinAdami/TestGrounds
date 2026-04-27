using GameServerCore.Enums;
using GameServerCore.Packets.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Quests
{
    public class ReachLevel3Quest : GameQuest
    {
        public ReachLevel3Quest(Game game) : base(game, "ReachLevel3") { }

        public override bool EvaluateMatchRequirements() => true;

        public override void RegisterActivationListeners()
        {
            Activate();
        }

        public override QuestDisplayGroup GetInfoQuestData(Champion player)
        {
            return null;
        }

        protected override void RegisterCompletionListeners()
        {
            foreach (var player in _game.PlayerManager.GetPlayers(false))
            {
                if (player.Champion != null)
                {
                    ApiEventManager.OnLevelUp.AddListener(this, player.Champion, CheckLevelUp, false);
                }
            }
        }

        private void CheckLevelUp(AttackableUnit unit)
        {
            if (unit is Champion champion && champion.Stats.Level >= 3)
            {
                Complete(champion, null);
            }
        }

        public override QuestDisplayGroup GetRewardOptions(Champion player, Champion winner, Champion loser)
        {
            if (player != winner) return null;

            var group = new QuestDisplayGroup();

            group.AddQuest(new UIQuestData
            {
                InternalName = "reward_gold",
                Objective = "Level 3 Reached!",
                Tooltip = "Click for +500 Gold",
                IsTip = true
            });

            group.AddQuest(new UIQuestData
            {
                InternalName = "reward_dance",
                Objective = "Level 3 Reached!",
                Tooltip = "Click to Dance!",
                IsTip = true
            });

            group.OnOptionSelected = (clickedData) =>
            {
                if (clickedData.InternalName == "reward_gold")
                {
                    winner.Stats.Gold += 500;
                    _game.ChatCommandManager.SendDebugMsgFormatted(Chatbox.DebugMsgType.INFO, $"{winner.Name} received 500 Gold!");
                }
                else if (clickedData.InternalName == "reward_dance")
                {
                    _game.PacketNotifier.NotifyS2C_PlayEmote(Emotions.DANCE, winner.NetId);
                    _game.ChatCommandManager.SendDebugMsgFormatted(Chatbox.DebugMsgType.INFO, $"{winner.Name} got the moves!");
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
