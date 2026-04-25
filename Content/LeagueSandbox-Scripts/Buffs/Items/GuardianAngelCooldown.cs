using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs
{
    class HasBeenRevived : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }
}

namespace Buffs
{
    class WillRevive : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        Particle p;
        ObjAIBase owner;

        public void OnActivate(ObjAIBase owner)
        {
            this.owner = owner;
            owner.AddStatModifier(StatsModifier);
            p = AddParticleTarget(owner, owner, "rebirthready.troy", owner, float.MaxValue, bone: "spine");
        }
        public void OnDeactivate(ObjAIBase owner)
        {
            RemoveParticle(p);
        }

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }
}

