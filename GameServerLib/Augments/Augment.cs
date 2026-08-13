using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Augments
{
    public abstract class Augment
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string IconPath { get; }

        protected Augment(string id, string name, string description, string iconPath)
        {
            Id = id;
            Name = name;
            Description = description;
            IconPath = iconPath;
        }

        public abstract void Apply(Champion target);
    }
}