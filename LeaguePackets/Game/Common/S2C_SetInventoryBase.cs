using LeaguePackets.Game.Common;

namespace LeaguePackets.Game
{
    /// <summary>
    /// Shared body of <see cref="S2C_SetInventory_Broadcast"/> (0x10C) and
    /// <see cref="S2C_SetInventory_MapView"/> (0x127): the wire layout is identical — the two
    /// packet IDs differ only in the client's distribution policy (broadcast vs map-view scope).
    /// </summary>
    public abstract class S2C_SetInventoryBase : GamePacket
    {
        // NOTE: 4.20 uses only first 9 but still sends all 10 for all 3 of the arrays
        private ItemData[] _items = new ItemData[10];
        private float[] _itemCooldowns = new float[10];
        private float[] _itemMaxCooldowns = new float[10];
        public ItemData[] Items => _items;
        public float[] ItemCooldowns => _itemCooldowns;
        public float[] ItemMaxCooldowns => _itemMaxCooldowns;

        protected override void ReadBody(ByteReader reader)
        {
            for (var i = 0; i < this.Items.Length; i++)
            {
                this.Items[i] = reader.ReadItemPacket();
            }
            for (var i = 0; i < this.ItemCooldowns.Length; i++)
            {
                this.ItemCooldowns[i] = reader.ReadFloat();
            }
            for (var i = 0; i < this.ItemMaxCooldowns.Length; i++)
            {
                this.ItemMaxCooldowns[i] = reader.ReadFloat();
            }
        }

        protected override void WriteBody(ByteWriter writer)
        {
            for (var i = 0; i < Items.Length; i++)
            {
                writer.WriteItemPacket(Items[i]);
            }
            for (var i = 0; i < ItemCooldowns.Length; i++)
            {
                writer.WriteFloat(ItemCooldowns[i]);
            }
            for (var i = 0; i < ItemMaxCooldowns.Length; i++)
            {
                writer.WriteFloat(ItemMaxCooldowns[i]);
            }
        }
    }
}
