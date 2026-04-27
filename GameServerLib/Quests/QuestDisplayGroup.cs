using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.Quests
{
    public class QuestDisplayGroup
    {
        public string SourceQuestId { get; set; }
        public List<UIQuestData> Quests { get; } = new List<UIQuestData>();
        public bool IsActionable { get; set; }
        public Action<UIQuestData> OnOptionSelected { get; set; }

        public void AddQuest(UIQuestData quest)
        {
            Quests.Add(quest);
        }
    }
}
