using LENet;
using Channel = GameServerCore.Packets.Enums.Channel;

namespace PacketDefinitions420
{
    // Owned exclusively by the network thread. Holds the live ENet Host plus
    // the per-client Peer[] and BlowFish[]. The peer slots are mutated by the
    // net thread on handshake / disconnect (and by the game thread *only* via
    // OutboundClearPeer commands processed here).
    internal sealed class NetworkSender
    {
        private readonly Peer[] _peers;
        private readonly BlowFish[] _blowfishes;
        private readonly Host _server;

        public NetworkSender(Peer[] peers, BlowFish[] blowfishes, Host server)
        {
            _peers = peers;
            _blowfishes = blowfishes;
            _server = server;
        }

        public Peer[] Peers => _peers;
        public BlowFish[] Blowfishes => _blowfishes;
        public Host Host => _server;

        public bool SendUnicast(int userId, byte[] source, Channel channel, PacketFlags flags)
        {
            if (0 <= userId && userId < _peers.Length && _peers[userId] != null)
            {
                byte[] temp = source.Length >= 8 ? _blowfishes[userId].Encrypt(source) : source;
                return _peers[userId].Send((byte)channel, new LENet.Packet(temp, flags)) == 0;
            }
            return false;
        }

        public void BroadcastRaw(byte[] data, Channel channel, PacketFlags flags)
        {
            _server.Broadcast((byte)channel, new LENet.Packet(data, flags));
        }

        public void ClearPeer(int clientId)
        {
            if (0 <= clientId && clientId < _peers.Length)
            {
                _peers[clientId] = null;
            }
        }

        public void Execute(OutboundCommand cmd)
        {
            switch (cmd)
            {
                case OutboundUnicast u:
                    SendUnicast(u.ClientId, u.Plaintext, u.Ch, u.Flags);
                    break;
                case OutboundBroadcastRaw b:
                    BroadcastRaw(b.Bytes, b.Ch, b.Flags);
                    break;
                case OutboundClearPeer c:
                    ClearPeer(c.ClientId);
                    break;
            }
        }
    }
}
