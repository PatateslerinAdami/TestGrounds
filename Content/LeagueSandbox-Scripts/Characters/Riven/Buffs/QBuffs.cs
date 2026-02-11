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
    internal class RivenTriCleaveSoundOne : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            SealSpellSlot(ownerSpell.CastInfo.Owner, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, true);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            SealSpellSlot(ownerSpell.CastInfo.Owner, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, false);
        }
    }

    internal class RivenTriCleaveBuffer : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            SetSpell(ownerSpell.CastInfo.Owner, "RivenTriCleaveBuffer", SpellSlotType.SpellSlots, 0).SetCooldown(0.1f);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var owner = ownerSpell.CastInfo.Owner;
            SetSpell(owner, "RivenTriCleave", SpellSlotType.SpellSlots, 0);
            if (owner.HasBuff("RivenTriCleaveBuffered"))
            {
                owner.RemoveBuffsWithName("RivenTriCleaveBuffered");
                if (owner.HasBuff("RivenTriCleave"))
                {
                    SetSpell(owner, "RivenTriCleaveBuffer", SpellSlotType.SpellSlots, 0).SetCooldown(0.25f);
                    AddBuff("RivenTriCleaveBufferB", 0.25f, 1, ownerSpell, owner, owner);
                }
            }
        }
    }

    internal class RivenTriCleave : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 3
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            buff.SetStatusEffect(StatusFlags.Ghosted, true);
            if (buff.StackCount < 3)
            {
                ownerSpell.SetCooldown(0.2f, true);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            buff.SetStatusEffect(StatusFlags.Ghosted, false);
        }
    }
}