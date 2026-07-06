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
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.STUN,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; }

        Particle stun;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            // Wire (Sion Q test replay, LOC_Stun.troy groups): flags 0x0020, bone
            // C_BuffBone_Glb_Center_Loc, bind = the stunned unit, TargetNetID = 0 —
            // so AddParticle (not AddParticleTarget) and the center buffbone, not "head".
            stun = AddParticle(ownerSpell.CastInfo.Owner, unit, "LOC_Stun", unit.Position, buff.Duration,
                bone: "C_BuffBone_Glb_Center_Loc");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}