using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoRArmorPen : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            IsHidden = false // Ca să îl vedem pe HUD
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private Particle swordGlow;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.Stats.ArmorPenetration.PercentBonus += 0.5f;
            swordGlow = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "Yasuo_Base_R_SwordGlow", unit, buff.Duration, 1, "r_hand");
            //swordGlow = AddParticleTarget(ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, "Yasuo_Base_R_SwordGlow", ownerSpell.CastInfo.Owner, buff.Duration, 1, "R_PARENTING_HAND_LOC");
        }   

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.Stats.ArmorPenetration.PercentBonus -= 0.5f;
            if (swordGlow != null) RemoveParticle(swordGlow);
        }
    }
}