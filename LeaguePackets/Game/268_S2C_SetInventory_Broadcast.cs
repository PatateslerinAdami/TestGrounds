namespace LeaguePackets.Game
{
    // Body shared with S2C_SetInventory_MapView — see S2C_SetInventoryBase (Game/Common).
    public class S2C_SetInventory_Broadcast : S2C_SetInventoryBase // 0x10C
    {
        public override GamePacketID ID => GamePacketID.S2C_SetInventory_Broadcast;
    }
}
