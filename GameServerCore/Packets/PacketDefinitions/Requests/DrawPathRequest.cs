using System.Numerics;

namespace GameServerCore.Packets.PacketDefinitions.Requests
{
    /// <summary>
    /// C2S_UnitSendDrawPath (0x106): a cursor-path point streamed by the client while draw-path
    /// mode is active on its champion (enabled via S2C_UnitSetDrawPathMode). NodeType: 0 = stroke
    /// start (key down), 1 = mid point (repeat, throttled by the mode's UpdateRate), 2 = stroke
    /// end (key up). Point is the cursor's world position.
    /// </summary>
    public class DrawPathRequest : ICoreRequest
    {
        public uint TargetNetID { get; }
        public byte NodeType { get; }
        public Vector3 Point { get; }

        public DrawPathRequest(uint targetNetId, byte nodeType, Vector3 point)
        {
            TargetNetID = targetNetId;
            NodeType = nodeType;
            Point = point;
        }
    }
}
