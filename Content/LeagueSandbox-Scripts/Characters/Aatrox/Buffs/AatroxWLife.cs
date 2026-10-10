using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class AatroxWLife : IBuffGameScript
    {
        private Particle _glow;

        protected virtual string Effect
        {
            get
            {
                return "W_Weaponlife";
            }
        }

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            var owner = (ObjAIBase)unit;
            _glow = AddParticleTarget(owner, owner, Spells.AatroxHelpers.Effect(owner, Effect), owner, bone: "weapon");
            _glow.isInfinite = true;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_glow);
        }
    }

    public class AatroxWONHLifeBuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }
}