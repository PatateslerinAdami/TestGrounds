using GameServerCore.Enums;

namespace AIScripts
{
    // TaskBuyItem.lua: walk to the fountain (regroup point) and buy items from a fixed list, cycling
    // through it. Priority is gold/distance-based — high (0.8) when within 3000u of the fountain and the
    // current item is affordable, tapering to 0.2 farther out; ×1.1 if gold is comfortably above the
    // price; capped at 0.9. SIMPLIFICATION: the shopping list is the Lua's fixed basic-item list (no
    // recipe combining / build paths).
    public class TaskBuyItem : BotTask
    {
        // Lua ITEMS_TO_BUY: Boots(1001), Health Potion(2003), Regrowth Pendant(1006), Faerie Charm(1007),
        // Dagger(1042), Cloth Armor(1029).
        private static readonly int[] ITEMS_TO_BUY = { 1001, 2003, 1006, 1007, 1042, 1029 };
        private const float MIN_DIST_TO_SHOP = 500.0f;

        private int _buyIndex;

        private int CurrentItem => ITEMS_TO_BUY[_buyIndex];
        private void AdvanceItem() => _buyIndex = (_buyIndex + 1) % ITEMS_TO_BUY.Length;

        public override void UpdatePriority(BotAI bot)
        {
            Priority = 0.0f;
            int price = bot.ItemPrice(CurrentItem);
            if (price <= 0)
            {
                AdvanceItem();   // invalid/free entry → skip to the next
                return;
            }
            if (bot.Gold < price)
            {
                return;
            }

            float mult = bot.Gold > 2 * price ? 1.1f : 1.0f;
            float dist = BotAI.Dist(bot.RegroupPos, bot.Position);
            if (dist < 3000.0f)
            {
                Priority = 0.8f * mult;
            }
            else if (dist < 6000.0f)
            {
                Priority = 6000.0f / dist * 0.2f * mult;
            }
            else
            {
                Priority = 0.2f * mult;
            }
            if (Priority > 0.9f)
            {
                Priority = 0.9f;
            }
        }

        public override void BeginTask(BotAI bot)
        {
            bot.StopAttacking();
            bot.SetBotState(AIState.AI_SHOP);
        }

        public override void Tick(BotAI bot)
        {
            float dist = BotAI.Dist(bot.RegroupPos, bot.Position);
            if (bot.State == AIState.AI_SHOP && dist > MIN_DIST_TO_SHOP)
            {
                bot.MoveToPoint(AIState.AI_MOVE, bot.RegroupPos);
            }
            else if (dist <= MIN_DIST_TO_SHOP)
            {
                if (bot.State == AIState.AI_MOVE)
                {
                    bot.StopMoving();
                    bot.SetBotState(AIState.AI_SHOP);
                }
                if (bot.Gold > bot.ItemPrice(CurrentItem) && bot.BuyItem(CurrentItem))
                {
                    AdvanceItem();
                }
            }
        }
    }
}
