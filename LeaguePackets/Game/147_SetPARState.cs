
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    /// <summary>
    /// Sets a unit's PAR (resource bar) visual STATE (GamePacketID 0x93). Decomp:
    /// PKT_SetPARState_s { mUnitID, int PARStateID }. The client swaps the unit's resource bar
    /// color + animated texture to the given state. Replay-verified: ~1.27M packets, values 0/1/2.
    /// </summary>
    public class SetPARState : GamePacket // 0x93
    {
        public override GamePacketID ID => GamePacketID.SetPARState;
        public uint UnitNetID { get; set; }
        /// <summary>
        /// Index into the per-PAR-type visual-state list (client config DATA\Menu_SC4\PARStates.ini:
        /// section = PAR type, keys State{n}Color / State{n}FadeColor / State{n}VideoPrefix). The meaning
        /// is PER PAR TYPE and not even consistently ordered — e.g. Heat: 0=normal,1=warning,2=overheat;
        /// Gnarfury: 0=calm,1=building,2=raging; BloodWell: 0=active,1=low (INVERTED). Use the
        /// champion-specific enums in GameServerCore.Enums (RumbleHeatState, GnarFuryState, …). There is
        /// deliberately NO global PARState enum. 0 = base look.
        /// </summary>
        public uint PARState { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            this.UnitNetID = reader.ReadUInt32();
            this.PARState = reader.ReadUInt32();
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteUInt32(UnitNetID);
            writer.WriteUInt32(PARState);
        }
    }
}
