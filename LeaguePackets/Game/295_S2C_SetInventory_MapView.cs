namespace LeaguePackets.Game
{
    // Body shared with S2C_SetInventory_Broadcast — see S2C_SetInventoryBase (Game/Common).
    public class S2C_SetInventory_MapView : S2C_SetInventoryBase // 0x127
    {
        public override GamePacketID ID => GamePacketID.S2C_SetInventory_MapView;
    }
}
