
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    public class S2C_ShowHealthBar : GamePacket // 0xCE
    {
        public override GamePacketID ID => GamePacketID.S2C_ShowHealthBar;
        // Client handler (S4 AIBaseClient.cpp:4784): bit0 SHOW → bHideHealthBar = !show; bit1
        // CHANGETYPE → re-create the unit-info renderer from HealthBarType (2 = hero-style bar,
        // 1 = unit-style bar, else no-op), applied only when ObserverTeamID == 0 or == the local
        // player's team. Wire reality (8425 packets across all 4.20 replays): every packet is
        // 7 bytes — bit1 is NEVER set and the type byte is always present (= 0); the 4.17 header
        // struct's fixed 11-byte form (observerTeam always present) is not what 4.20 sends, so
        // the conditional ObserverTeamID below matches the observed wire. Riot uses this packet
        // ONLY as a show/hide toggle: heroes get hide-on-load/show-on-start, and dynamic cases
        // like Karthus' Death Defied (replay-verified in KarthusDeathDefiedBuff). Persistent
        // barless units (TestCubeRender*) never receive it — their CharData carries
        // NeverRender=1 and the client hides model + unit info from that alone.
        public bool ShowHealthBar { get; set;}
        public bool ChangeHealthBarType { get; set; }
        public byte HealthBarType { get; set; }
        public uint ObserverTeamID { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            byte bitfield = reader.ReadByte();
            this.ShowHealthBar = (bitfield & 1) != 0;
            this.ChangeHealthBarType = (bitfield & 2) != 0;
            this.HealthBarType = reader.ReadByte(); // should be writen only when ObserverTeam
            if (this.ChangeHealthBarType)
            {
                this.ObserverTeamID = reader.ReadUInt32();
            }
        }
        protected override void WriteBody(ByteWriter writer)
        {
            byte bitfield = 0;
            if(ShowHealthBar)
            {
                bitfield |= 1;
            }
            if(ChangeHealthBarType)
            {
                bitfield |= 2;
            }
            writer.WriteByte(bitfield);
            writer.WriteByte(HealthBarType);
            if (ChangeHealthBarType)
            {
                writer.WriteUInt32(ObserverTeamID);
            }
        }
    }
}
