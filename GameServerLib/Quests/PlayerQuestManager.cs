using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace LeagueSandbox.GameServer.Quests
{
    public class PlayerQuestManager
    {
        private readonly Champion _owner;
        private readonly Game _game;

        private readonly Dictionary<string, QuestDisplayGroup> _activeObjectives;

        private readonly Dictionary<string, QuestDisplayGroup> _activeInfoTips; 
        private readonly Queue<QuestDisplayGroup> _actionableTipsQueue;        

        private QuestDisplayGroup _currentlyDisplayedActionableTip;
        private bool _infoTipsHidden = false;
        private bool _isClientReady = false;

        public PlayerQuestManager(Game game, Champion owner)
        {
            _game = game;
            _owner = owner;
            _activeObjectives = new Dictionary<string, QuestDisplayGroup>();
            _activeInfoTips = new Dictionary<string, QuestDisplayGroup>();
            _actionableTipsQueue = new Queue<QuestDisplayGroup>();
        }

        public uint GetNextQuestId() => _game.NetworkIdManager.GetNewNetId();

        public void SyncActiveQuests()
        {
            _isClientReady = true;

            foreach (var kvp in _activeObjectives)
            {
                SendGroupPackets(kvp.Value, QuestCommand.Activate, false);
            }

            if (_currentlyDisplayedActionableTip != null)
            {
                SendGroupPackets(_currentlyDisplayedActionableTip, QuestCommand.Activate, false);
            }
            else if (!_infoTipsHidden)
            {
                foreach (var kvp in _activeInfoTips)
                {
                    SendGroupPackets(kvp.Value, QuestCommand.Activate, false);
                }
            }
        }

        public void AddQuest(QuestDisplayGroup group)
        {
            bool isObjective = group.Quests.Any(q => !q.IsTip);

            if (isObjective)
            {
                _activeObjectives[group.SourceQuestId] = group;
                if (_isClientReady) SendGroupPackets(group, QuestCommand.Activate, false);
            }
            else
            {
                if (group.IsActionable)
                {
                    _actionableTipsQueue.Enqueue(group);
                    if (_isClientReady) UpdateTipDisplay();
                }
                else
                {
                    _activeInfoTips[group.SourceQuestId] = group;
                    if (_isClientReady && !_infoTipsHidden)
                    {
                        SendGroupPackets(group, QuestCommand.Activate, false);
                    }
                }
            }
        }

        public void CompleteQuest(string sourceQuestId, bool success)
        {
            if (_activeObjectives.TryGetValue(sourceQuestId, out var objGroup))
            {
                if (_isClientReady) SendGroupPackets(objGroup, QuestCommand.Complete, success);
                _activeObjectives.Remove(sourceQuestId);
                return;
            }

            if (_activeInfoTips.TryGetValue(sourceQuestId, out var infoGroup))
            {
                if (_isClientReady && !_infoTipsHidden)
                {
                    SendGroupPackets(infoGroup, QuestCommand.Complete, success);
                }
                _activeInfoTips.Remove(sourceQuestId);
                return;
            }

            if (_currentlyDisplayedActionableTip != null && _currentlyDisplayedActionableTip.SourceQuestId == sourceQuestId)
            {
                if (_isClientReady) SendGroupPackets(_currentlyDisplayedActionableTip, QuestCommand.Complete, success);
                _actionableTipsQueue.Dequeue();
                _currentlyDisplayedActionableTip = null;
                if (_isClientReady) UpdateTipDisplay();
                return;
            }

            var list = _actionableTipsQueue.ToList();
            if (list.RemoveAll(g => g.SourceQuestId == sourceQuestId) > 0)
            {
                _actionableTipsQueue.Clear();
                foreach (var item in list) _actionableTipsQueue.Enqueue(item);
            }
        }
        //Not sure if GameScriptTimer are necessary but i dont see a harm and better be safe than sorry.
        private void UpdateTipDisplay()
        {
            if (!_isClientReady) return;

            if (_actionableTipsQueue.Count > 0)
            {
                var nextActionable = _actionableTipsQueue.Peek();
                if (_currentlyDisplayedActionableTip == nextActionable) return;

                if (!_infoTipsHidden && _activeInfoTips.Count > 0)
                {
                    //Hide info tips
                    foreach (var infoGroup in _activeInfoTips.Values)
                    {
                        SendGroupPackets(infoGroup, QuestCommand.Remove, false);
                    }
                    _infoTipsHidden = true;

                    _currentlyDisplayedActionableTip = nextActionable;

                    _game.AddGameScriptTimer(new GameScriptTimer(0.2f, () =>
                    {
                        if (_currentlyDisplayedActionableTip == nextActionable)
                        {
                            SendGroupPackets(_currentlyDisplayedActionableTip, QuestCommand.Activate, false);
                        }
                    }));
                }
                else
                {
                    // If we are swapping one actionable tip for another
                    if (_currentlyDisplayedActionableTip != null)
                    {
                        SendGroupPackets(_currentlyDisplayedActionableTip, QuestCommand.Remove, false);
                    }

                    _currentlyDisplayedActionableTip = nextActionable;

                    _game.AddGameScriptTimer(new GameScriptTimer(0.2f, () =>
                    {
                        if (_currentlyDisplayedActionableTip == nextActionable)
                        {
                            SendGroupPackets(_currentlyDisplayedActionableTip, QuestCommand.Activate, false);
                        }
                    }));
                }
            }
            else
            {
                if (_currentlyDisplayedActionableTip != null)
                {
                    SendGroupPackets(_currentlyDisplayedActionableTip, QuestCommand.Remove, false);
                    _currentlyDisplayedActionableTip = null;
                }

                if (_infoTipsHidden)
                {
                    _infoTipsHidden = false;

                    _game.AddGameScriptTimer(new GameScriptTimer(0.2f, () =>
                    {
                        if (_actionableTipsQueue.Count == 0)
                        {
                            foreach (var infoGroup in _activeInfoTips.Values)
                            {
                                SendGroupPackets(infoGroup, QuestCommand.Activate, false);
                            }
                        }
                    }));
                }
            }
        }

        public void OnQuestClicked(uint clickedQuestId)
        {
            foreach (var kvp in _activeObjectives)
            {
                var group = kvp.Value;
                var clickedQuest = group.Quests.FirstOrDefault(q => q.QuestId == clickedQuestId);
                if (clickedQuest != null)
                {
                    if (group.IsActionable)
                    {
                        group.OnOptionSelected?.Invoke(clickedQuest);
                        foreach (var quest in group.Quests)
                        {
                            SendSingleQuestPacket(quest, quest.QuestId == clickedQuestId ? QuestCommand.Complete : QuestCommand.Remove, true);
                        }
                        _activeObjectives.Remove(kvp.Key);
                    }
                    return;
                }
            }

            foreach (var kvp in _activeInfoTips)
            {
                var clickedQuest = kvp.Value.Quests.FirstOrDefault(q => q.QuestId == clickedQuestId);
                if (clickedQuest != null)
                {
                    SendSingleQuestPacket(clickedQuest, QuestCommand.Remove, true);
                    kvp.Value.Quests.Remove(clickedQuest);
                    if (kvp.Value.Quests.Count == 0)
                    {
                        _activeInfoTips.Remove(kvp.Key);
                    }
                    return;
                }
            }

            if (_currentlyDisplayedActionableTip != null)
            {
                var clickedQuest = _currentlyDisplayedActionableTip.Quests.FirstOrDefault(q => q.QuestId == clickedQuestId);
                if (clickedQuest != null)
                {
                    _currentlyDisplayedActionableTip.OnOptionSelected?.Invoke(clickedQuest);

                    foreach (var quest in _currentlyDisplayedActionableTip.Quests)
                    {
                        SendSingleQuestPacket(quest, quest.QuestId == clickedQuestId ? QuestCommand.Complete : QuestCommand.Remove, true);
                    }

                    _actionableTipsQueue.Dequeue();
                    _currentlyDisplayedActionableTip = null;
                    UpdateTipDisplay();
                }
            }
        }

        private void SendGroupPackets(QuestDisplayGroup group, QuestCommand command, bool success)
        {
            foreach (var quest in group.Quests)
            {
                SendSingleQuestPacket(quest, command, success);
            }
        }

        private void SendSingleQuestPacket(UIQuestData quest, QuestCommand command, bool success)
        {
            if (quest.IsTip)
            {
                byte tipCommand = (command == QuestCommand.Activate) ? (byte)0 : (byte)1;
                _game.PacketNotifier.NotifyS2C_HandleTipUpdate(
                    _owner.ClientId,
                    quest.Objective,
                    quest.Tooltip,
                    quest.Icon,
                    tipCommand,
                    _owner.NetId,
                    quest.QuestId
                );
            }
            else
            {
                _game.PacketNotifier.NotifyS2C_HandleQuestUpdate(
                    _owner.ClientId,
                    quest.QuestId,
                    quest.Objective,
                    quest.Tooltip,
                    quest.Icon,
                    (byte)command,
                    quest.QuestType,
                    success
                );
            }
        }
    }
}
