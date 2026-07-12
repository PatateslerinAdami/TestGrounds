
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LeaguePackets.Game.Common;

namespace LeaguePackets.Game
{
    public class NPC_CastSpellAns : GamePacket // 0xB5
    {
        public override GamePacketID ID => GamePacketID.NPC_CastSpellAns;
        public int CasterPositionSyncID { get; set; }
        // Bitfield bit 0 — client packet-routing flag (S4 obj_AI_Base_PImpl_Int.cpp:2987+):
        // false = fresh cast (client creates a new SpellInstanceClient); true = continuation of
        // the cast already in flight — the client resolves the existing instance via
        // SpellbookRouter by SpellNetID and continues it (drops the packet on mismatch).
        // Required true on the charge-FIRE re-broadcast of charge spells (Varus Q: charge-start
        // false, fire true — replay-verified), else the client spawns a broken fresh cast.
        public bool IsContinuationCast { get; set; }
        public CastInfo CastInfo { get; set; } = new CastInfo();

        protected override void ReadBody(ByteReader reader)
        {

            this.CasterPositionSyncID = reader.ReadInt32();

            byte bitfield = reader.ReadByte();
            this.IsContinuationCast = (bitfield & 1) != 0;

            this.CastInfo = reader.ReadCastInfo();
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteInt32(CasterPositionSyncID);

            byte bitfield = 0;
            if(IsContinuationCast)
            {
                bitfield |= 1;
            }
            writer.WriteByte(bitfield);

            writer.WriteCastInfo(CastInfo);
        }
    }
}
