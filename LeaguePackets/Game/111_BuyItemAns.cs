
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LeaguePackets.Game.Common;

namespace LeaguePackets.Game
{
    public class BuyItemAns : GamePacket // 0x6F
    {
        public override GamePacketID ID => GamePacketID.BuyItemAns;
        public ItemData Item { get; set; } = new ItemData();

        // The trailing byte is a bitfield with a single named flag (mac decomp PKT_BuyItemAns_s:
        // ITEMCALLOUT_MASK = 0x40, shift 6); the other 7 bits are unused. When set, the client fires
        // the item-purchase callout (HeroInventory::ItemCallout / ParamsItemCallout — the "bought X"
        // ally announcement).
        private const byte ItemCalloutMask = 0x40;

        /// <summary>Whether to trigger the client's item-purchase callout announcement for this buy.</summary>
        public bool ItemCallout { get; set; }

        protected override void ReadBody(ByteReader reader)
        {
            this.Item = reader.ReadItemPacket();
            byte bitfield = reader.ReadByte();
            this.ItemCallout = (bitfield & ItemCalloutMask) != 0;
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteItemPacket(Item);
            writer.WriteByte((byte)(ItemCallout ? ItemCalloutMask : 0));
        }
    }
}
