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
    class RivenFeint : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            BuffType = BuffType.COMBAT_ENCHANCER
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        Particle pbuff, pbuff1, pbuff2;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var owner = ownerSpell.CastInfo.Owner as Champion;
            pbuff = AddParticleTarget(owner, owner, "Riven_Base_E_Shield.troy", owner, buff.Duration);
            pbuff1 = AddParticleTarget(owner, owner, "Riven_Base_E_Mis.troy", owner, buff.Duration);
            pbuff2 = AddParticleTarget(owner, owner, "Riven_Base_E_Interupt.troy", owner, buff.Duration);
            unit.TakeShield(200f, 200f, true);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(pbuff);
            RemoveParticle(pbuff1);
            RemoveParticle(pbuff2);
            unit.TakeShield(-200f, -200f, true);
        }
    }
}