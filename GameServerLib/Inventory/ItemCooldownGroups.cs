namespace LeagueSandbox.GameServer.Inventory
{
    public static class ItemCooldownGroups
    {
        public static string Get(string spellName) => spellName == "ItemDervishBlade" || spellName == "ItemMercurial" ? "QuicksilverSash" : spellName;
    }
}
