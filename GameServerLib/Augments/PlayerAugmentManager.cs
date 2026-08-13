using GameServerCore.Enums;
using LeagueSandbox.GameServer.Augments;
using LeagueSandbox.GameServer.Chatbox;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Quests;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LeagueSandbox.GameServer.Augments
{
    public class PlayerAugmentManager
    {
        private readonly Game _game;
        private readonly Champion _owner;
        private readonly List<Augment> _ownedAugments;
        private readonly List<Augment> _pendingAugments; 
        private readonly Random _rng;

        public PlayerAugmentManager(Game game, Champion owner)
        {
            _game = game;
            _owner = owner;
            _ownedAugments = new List<Augment>();
            _pendingAugments = new List<Augment>();
            _rng = new Random();
        }

        public void OfferAugments(int count, params string[] poolNames)
        {
            if (poolNames == null || poolNames.Length == 0)
            {
                poolNames = new[] { "Base" };
            }

            var availableAugments = AugmentPool.GetAugmentsFromPools(poolNames)
                .Where(a => !_ownedAugments.Contains(a) && !_pendingAugments.Contains(a))
                .ToList();

            var choices = availableAugments
                .OrderBy(a => _rng.Next())
                .Take(count)
                .ToList();

            if (choices.Count == 0)
            {
                return;
            }

            _pendingAugments.AddRange(choices);

            var group = new QuestDisplayGroup
            {
                SourceQuestId = "Augment_Offer_" + _game.GameTime,
                IsActionable = true,
                OnOptionSelected = (selectedQuest) =>
                {
                    var chosenAugment = choices.FirstOrDefault(a => a.Id == selectedQuest.InternalName);
                    if (chosenAugment != null)
                    {
                        chosenAugment.Apply(_owner);
                        _ownedAugments.Add(chosenAugment);

                        _game.ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, $"{_owner.Name} selected: {chosenAugment.Name}");
                    }

                    foreach (var choice in choices)
                    {
                        _pendingAugments.Remove(choice);
                    }
                }
            };

            foreach (var choice in choices)
            {
                group.AddQuest(new UIQuestData
                {
                    QuestId = _owner.PlayerQuestManager.GetNextQuestId(),
                    InternalName = choice.Id,
                    Objective = choice.Name,
                    Tooltip = choice.Description,
                    Icon = choice.IconPath,
                    QuestType = QuestType.Primary,
                    IsTip = true
                });
            }

            _owner.PlayerQuestManager.AddQuest(group);
        }
    }
}