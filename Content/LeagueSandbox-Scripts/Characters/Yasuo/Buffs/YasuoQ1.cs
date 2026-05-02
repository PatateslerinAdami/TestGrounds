using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Buffs
{
    internal class YasuoQ : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            BuffType = BuffType.COMBAT_ENCHANCER,
            IsHidden = false,
        };

        public StatsModifier StatsModifier { get; private set; }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (unit is ObjAIBase ai)
            {
                SetSpell(ai, "YasuoQ2W", SpellSlotType.SpellSlots, 0);

                int charLevel = ai.Stats.Level;
                int trueQLevel = charLevel >= 9 ? 5 : charLevel >= 7 ? 4 : charLevel >= 5 ? 3 : charLevel >= 4 ? 2 : 1;

                float baseCooldown = 5.25f - (0.25f * trueQLevel);
                
                float bonusAS = ai.Stats.AttackSpeedMultiplier.Total - 1.0f; 
                if (bonusAS < 0) bonusAS = 0;

                float cdReduction = bonusAS / 1.67f;
                float finalCooldown = baseCooldown * (1f - cdReduction);
                if (finalCooldown < 1.33f) finalCooldown = 1.33f;
                
                ai.Spells[0].SetCooldown(finalCooldown, true);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (unit is ObjAIBase ai)
            {
                if (ai.Spells[0].SpellName == "YasuoQ2W")
                {
                    SetSpell(ai, "YasuoQW", SpellSlotType.SpellSlots, 0);
                }
            }
        }
    }
}