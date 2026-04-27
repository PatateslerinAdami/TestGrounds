using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Quests
{
    public class KillZacQuest : GameQuest
    {
        private Champion _zac;

        public KillZacQuest(Game game) : base(game, "KillZac") { }

        public override bool EvaluateMatchRequirements()
        {
            foreach (var player in _game.PlayerManager.GetPlayers(false))
            {
                if (player.Champion.Model == "Zac")
                {
                    _zac = player.Champion;
                    break;
                }
            }
            return _zac != null;
        }

        public override void RegisterActivationListeners()
        {
            Activate();
        }

        public override QuestDisplayGroup GetInfoQuestData(Champion player)
        {
            if (player.Team != _zac.Team)
            {
                var group = new QuestDisplayGroup();
                group.AddQuest(new UIQuestData
                {
                    Objective = "game_quest_name_zacbounty",
                    Tooltip = "game_quest_description_zacbounty_enemy",
                    IsTip = false
                });
                return group;
            }
            return null;
        }

        protected override void RegisterCompletionListeners()
        {
            ApiEventManager.OnDeath.AddListener(this, _zac, OnZacDeath, false);
        }

        private void OnZacDeath(DeathData deathData)
        {
            if (deathData.Killer is Champion killer && killer.Team != _zac.Team)
            {
                Complete(killer, _zac);
            }
        }

        public override QuestDisplayGroup GetRewardOptions(Champion player, Champion winner, Champion loser)
        {
            if (player != winner) return null;

            var group = new QuestDisplayGroup();

            group.AddQuest(new UIQuestData
            {
                InternalName = "reward_size",
                Objective = "Zac Defeated!",
                Tooltip = "game_quest_description_zachunt",
                IsTip = true
            });

            group.OnOptionSelected = (clickedData) =>
            {
                if (clickedData.InternalName == "reward_size")
                {
                    winner.Stats.Size.PercentBonus += 0.8f;

                    _game.ChatCommandManager.SendDebugMsgFormatted(Chatbox.DebugMsgType.INFO, $"{winner.Name} grew larger!");
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
