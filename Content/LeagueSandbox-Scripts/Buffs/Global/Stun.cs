using GameServerCore.Enums;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Buffs
{
    internal class Stun : IBuffGameScript
    {
        private Particle _stun;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.STUN,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; }

       

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _stun = AddParticle(buff.SourceUnit, unit, "LOC_Stun", unit.Position, buff.Duration,
                bone: "C_BuffBone_Glb_Center_Loc");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_stun);
        }
    }
}