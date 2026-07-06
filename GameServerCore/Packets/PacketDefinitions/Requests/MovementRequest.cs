using GameServerCore.Enums;
using System.Collections.Generic;
using System.Numerics;

namespace GameServerCore.Packets.PacketDefinitions.Requests
{
    public class MovementRequest : ICoreRequest
    {
        public OrderType OrderType { get; }
        public Vector2 Position { get; }
        public uint TargetNetID { get; }
        public uint TeleportNetID { get;  }
        public bool HasTeleportID { get;  }
        public byte TeleportID { get;  }
        public List<Vector2> Waypoints { get; }
        // The unit the client is commanding (GamePacket header SenderNetID). Normally the player's
        // champion, but for pet control it is the controlled pet's NetID — the server must route the
        // order to that unit, not always the champion.
        public uint SenderNetID { get; }

        public MovementRequest(OrderType orderType, Vector2 position, uint targetNetId, uint teleportNetId, bool hasTeleportId, List<Vector2> waypoints, uint senderNetId = 0)
        {
            OrderType = orderType;
            Position = position;
            TargetNetID = targetNetId;
            TeleportNetID = teleportNetId;
            HasTeleportID = hasTeleportId;
            Waypoints = waypoints;
            SenderNetID = senderNetId;
        }
    }
}
