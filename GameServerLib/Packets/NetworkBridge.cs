using System.Collections.Concurrent;
using System.Threading;
using GameServerCore.Enums;
using GameServerCore.Packets.PacketDefinitions;
using Channel = GameServerCore.Packets.Enums.Channel;
using PacketFlags = LENet.PacketFlags;

namespace PacketDefinitions420
{
    // Two-thread split: the game thread owns game state, the net thread owns
    // ENet's Host and Peer[]. Everything that crosses the boundary travels
    // through this bridge. ConcurrentQueue gives us lock-free MPSC/SPSC
    // enqueue+dequeue; OutboundSignal lets the game thread wake the net
    // thread out of an idle HostService poll without spinning.
    public sealed class NetworkBridge
    {
        public readonly ConcurrentQueue<InboundEvent> Inbound = new();
        public readonly ConcurrentQueue<OutboundCommand> Outbound = new();
        public readonly AutoResetEvent OutboundSignal = new(false);
        public volatile bool Stop;

        public void EnqueueOutbound(OutboundCommand cmd)
        {
            Outbound.Enqueue(cmd);
            OutboundSignal.Set();
        }
    }

    public abstract record InboundEvent(int ClientId);
    public sealed record InboundRequest(int ClientId, ICoreRequest Request) : InboundEvent(ClientId);
    public sealed record InboundDisconnect(int ClientId) : InboundEvent(ClientId);

    public abstract record OutboundCommand;
    public sealed record OutboundUnicast(int ClientId, byte[] Plaintext, Channel Ch, PacketFlags Flags) : OutboundCommand;
    // Sub-8-byte broadcast goes through Host.Broadcast (no per-peer encryption);
    // the only real-world path for this is the Disconnected pseudo-packet.
    public sealed record OutboundBroadcastRaw(byte[] Bytes, Channel Ch, PacketFlags Flags) : OutboundCommand;
    // Marks a slot as no longer connected. Pushed by the game thread when
    // HandleExit fires (no real ENet DISCONNECT in that path).
    public sealed record OutboundClearPeer(int ClientId) : OutboundCommand;
}
