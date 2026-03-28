using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    class AatroxR : IBuffGameScript
    {
        Particle jetR, jetL;
        private static readonly Dictionary<string, string> UltAnimationOverrides = new()
        {
            { "run", "run_ult" },
            { "idle1", "idle_ult" },
            { "Attack1", "Attack1_ULT" },
            { "Attack2", "Attack2_ULT" },
            { "Attack3", "Attack3_ULT" },
            { "Spell3", "Spell3_ULT" },
            { "Spell2", "spell2_ULT" }
        };

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (unit is Champion c)
            {
                if (c.SkinID == 2)
                {
                    jetR = AddParticleTarget(c, c, "Aatrox_Skin02_R_Engine_01", c, bone: "R_AfterBurner");
                    jetR.isInfinite = true;
                    jetL = AddParticleTarget(c, c, "Aatrox_Skin02_R_Engine_01", c, bone: "L_AfterBurner");
                    jetL.isInfinite = true;
                }

                unit.SetAnimStates(UltAnimationOverrides, this);

                if (unit.HasBuff("AatroxWLife"))
                {
                    OverrideAnimation(unit, "spell2_ULT", "Attack3", "AatroxW");
                    OverrideAnimation(unit, "spell2_ULT", "Attack6", "AatroxW");
                }

                StatsModifier.AttackSpeed.PercentBonus = (0.4f + (0.1f * (ownerSpell.CastInfo.SpellLevel - 1))) * buff.StackCount;
                StatsModifier.Range.FlatBonus = 175f * buff.StackCount;

                unit.AddStatModifier(StatsModifier);

                if (c.AutoAttackSpell != null)
                {
                    string currentAA = c.AutoAttackSpell.SpellName;
                    if (currentAA == "AatroxBasicAttack") c.SetAutoAttackSpell("AatroxBasicAttack4", false);
                    else if (currentAA == "AatroxBasicAttack2") c.SetAutoAttackSpell("AatroxBasicAttack5", false);
                    else if (currentAA == "AatroxBasicAttack3") c.SetAutoAttackSpell("AatroxBasicAttack6", false);
                }
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (unit is Champion c)
            {
                if (c.SkinID == 2)
                {
                    jetR.SetToRemove();
                    jetL.SetToRemove();
                }
                unit.RemoveAnimStates(this);

                if (unit.HasBuff("AatroxWLife"))
                {
                    OverrideAnimation(unit, "Spell2", "Attack3", "AatroxW");
                    OverrideAnimation(unit, "Spell2", "Attack6", "AatroxW");
                }

                if (c.AutoAttackSpell != null)
                {
                    string currentAA = c.AutoAttackSpell.SpellName;
                    if (currentAA == "AatroxBasicAttack4") c.SetAutoAttackSpell("AatroxBasicAttack", false);
                    else if (currentAA == "AatroxBasicAttack5") c.SetAutoAttackSpell("AatroxBasicAttack2", false);
                    else if (currentAA == "AatroxBasicAttack6") c.SetAutoAttackSpell("AatroxBasicAttack3", false);
                }
            }
        }
    }
}