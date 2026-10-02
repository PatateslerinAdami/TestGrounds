using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public abstract class SpellbladeItem : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        protected ObjAIBase Owner;
        public virtual void OnActivate(ObjAIBase owner)
        {
            Owner = owner;
            foreach (short slot in new short[]
            {
                0,
                1,
                2,
                3
            }

            )
            {
                if (owner.Spells.TryGetValue(slot, out var spell))
                {
                    ApiEventManager.OnSpellCast.AddListener(this, spell, Cast);
                }
            }
        }

        private void Cast(Spell spell)
        {
            if (!Owner.IsDead && spell.Script.ScriptMetadata.TriggersSpellCasts
                && !Owner.HasBuff("ItemSpellbladeCooldown"))
            {
                AddBuff("ItemSpellblade", 10, 1, spell, Owner, Owner);
            }
        }

        public virtual void OnUpdate(float diff)
        {
        }

        public virtual void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            if (!owner.Inventory.HasItemWithID(3057) && !owner.Inventory.HasItemWithID(3078)
                && !owner.Inventory.HasItemWithID(3100)
                && !owner.Inventory.HasItemWithID(3025))
            {
                RemoveBuff(owner, "ItemSpellblade");
            }
        }
    }

    public class ItemID_3057 : SpellbladeItem
    {
    }

    public class ItemID_3100 : SpellbladeItem
    {
        public override void OnUpdate(float diff)
        {
            ItemHelpers.ItemEffectHelpers.UpdateTooltip(Owner, 3100, 0, Owner.Stats.AbilityPower.Total * 0.5f);
            ItemHelpers.ItemEffectHelpers.UpdateTooltip(Owner, 3100, 1, Owner.Stats.AttackDamage.BaseValue * 0.75f);
        }
    }

    public class ItemID_3025 : SpellbladeItem
    {
        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            StatsModifier.CooldownReduction.FlatBonus = -0.1f;
            owner.AddStatModifier(StatsModifier);
        }
    }
}
