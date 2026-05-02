using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoRLeashParticle : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData { BuffType = BuffType.INTERNAL, IsHidden = true };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        
        private Particle leashParticle;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            leashParticle = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "Yasuo_Base_R_Beam", unit, buff.Duration);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (leashParticle != null) RemoveParticle(leashParticle);
        }
    }
}