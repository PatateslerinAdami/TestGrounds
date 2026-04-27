namespace GameServerCore.Enums
{
    public class UIQuestData
    {
        public uint QuestId { get; set; }
        public string InternalName { get; set; }
        public string Objective { get; set; }
        public string Tooltip { get; set; }
        public string Icon { get; set; }
        public QuestType QuestType { get; set; }
        public bool IsTip { get; set; }
    }
}
