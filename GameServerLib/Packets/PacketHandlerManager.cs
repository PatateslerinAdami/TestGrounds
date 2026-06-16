using LENet;
using GameServerCore.Enums;
using GameServerCore.Packets.Handlers;
using GameServerCore.Packets.PacketDefinitions;
using LeaguePackets;
using LeaguePackets.Game.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Channel = GameServerCore.Packets.Enums.Channel;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.Players;
using LeagueSandbox.GameServer;

namespace PacketDefinitions420
{
    /// <summary>
    /// Class containing all functions related to sending and receiving packets.
    /// After the network-thread split, all "send" methods on this class merely
    /// enqueue an <see cref="OutboundCommand"/> onto <see cref="NetworkBridge.Outbound"/>;
    /// the actual encryption + ENet send is performed by the network thread via
    /// <see cref="NetworkSender"/>. Handshake replies are the one exception: the
    /// handshake itself runs on the network thread, so it can still call the
    /// sender directly for guaranteed in-order delivery.
    /// </summary>
    public class PacketHandlerManager
    {
        private delegate ICoreRequest RequestConvertor(byte[] data);
        private readonly Dictionary<Tuple<GamePacketID, Channel>, RequestConvertor> _gameConvertorTable;
        private readonly Dictionary<LoadScreenPacketID, RequestConvertor> _loadScreenConvertorTable;
        private readonly Dictionary<GamePacketID, RequestConvertor> _fallbackConvertorTable;

        // Net-thread-owned state lives behind _sender. PacketHandlerManager
        // never touches _peers[] or _blowfishes[] directly anymore — except in
        // handshake which already runs on the network thread.
        internal readonly NetworkSender _sender;
        internal readonly NetworkBridge _bridge;

        private readonly PlayerManager _playerManager;
        private readonly Game _game;

        private readonly NetworkHandler<ICoreRequest> _netReq;
        private readonly NetworkHandler<ICoreRequest> _netResp;

        internal PacketHandlerManager(NetworkSender sender, NetworkBridge bridge, Game game, NetworkHandler<ICoreRequest> netReq, NetworkHandler<ICoreRequest> netResp)
        {
            _sender = sender;
            _bridge = bridge;
            _game = game;
            _playerManager = _game.PlayerManager;
            _netReq = netReq;
            _netResp = netResp;
            _gameConvertorTable = new Dictionary<Tuple<GamePacketID, Channel>, RequestConvertor>();
            _loadScreenConvertorTable = new Dictionary<LoadScreenPacketID, RequestConvertor>();
            _fallbackConvertorTable = new Dictionary<GamePacketID, RequestConvertor>();
            InitializePacketConvertors();
        }

        internal void InitializePacketConvertors()
        {
            foreach(var m in typeof(PacketReader).GetMethods())
            {
                foreach (Attribute attr in m.GetCustomAttributes(true))
                {
                    if (attr is PacketType)
                    {
                        if (((PacketType)attr).ChannelId == Channel.CHL_LOADING_SCREEN || ((PacketType)attr).ChannelId == Channel.CHL_COMMUNICATION)
                        {
                            var method = (RequestConvertor)Delegate.CreateDelegate(typeof(RequestConvertor), m);
                            _loadScreenConvertorTable.Add(((PacketType)attr).LoadScreenPacketId, method);
                        }
                        else
                        {
                            var key = new Tuple<GamePacketID, Channel>(((PacketType)attr).GamePacketId, ((PacketType)attr).ChannelId);
                            var method = (RequestConvertor)Delegate.CreateDelegate(typeof(RequestConvertor), m);
                            _gameConvertorTable.Add(key, method);
                            if (!_fallbackConvertorTable.ContainsKey(key.Item1))
                            {
                                _fallbackConvertorTable.Add(key.Item1, method);
                            }
                        }
                    }
                }
            }
        }

        private RequestConvertor GetConvertor(LoadScreenPacketID packetId)
        {
            var packetsHandledWhilePaused = new List<LoadScreenPacketID>
            {
                LoadScreenPacketID.RequestJoinTeam,
                LoadScreenPacketID.Chat
            };

            if (_game.IsPaused && !packetsHandledWhilePaused.Contains(packetId))
            {
                return null;
            }

            if (_loadScreenConvertorTable.ContainsKey(packetId))
            {
                return _loadScreenConvertorTable[packetId];
            }

            return null;

        }

        private RequestConvertor GetConvertor(GamePacketID packetId, Channel channelId)
        {
            var packetsHandledWhilePaused = new List<GamePacketID>
            {
                GamePacketID.Dummy,
                GamePacketID.SynchSimTimeC2S,
                GamePacketID.ResumePacket,
                GamePacketID.C2S_QueryStatusReq,
                GamePacketID.C2S_ClientReady,
                GamePacketID.C2S_Exit,
                GamePacketID.World_SendGameNumber,
                GamePacketID.SendSelectedObjID,
                GamePacketID.C2S_CharSelected,

                // The next two are required to reconnect
                GamePacketID.SynchVersionC2S,
                GamePacketID.C2S_Ping_Load_Info,

                // The next 5 are not really needed when reconnecting,
                // but they don't do much harm either
                GamePacketID.C2S_UpdateGameOptions,
                GamePacketID.OnReplication_Acc,
                GamePacketID.C2S_StatsUpdateReq,
                GamePacketID.World_SendCamera_Server,
                GamePacketID.C2S_OnTipEvent
            };
            if (_game.IsPaused && !packetsHandledWhilePaused.Contains(packetId))
            {
                return null;
            }
            var key = new Tuple<GamePacketID, Channel>(packetId, channelId);
            if (_gameConvertorTable.ContainsKey(key))
            {
                return _gameConvertorTable[key];
            }
            if (_fallbackConvertorTable.ContainsKey(packetId))
            {
                return _fallbackConvertorTable[packetId];
            }
            return null;
        }

        private void PrintPacket(byte[] buffer, string str)
        {
            // FIXME: currently lock disabled, not needed?
            Console.Write(str);
            foreach (var b in buffer)
            {
                Console.Write(b.ToString("X2") + " ");
            }

            Console.WriteLine("");
            Console.WriteLine("--------");
        }

        // -------- Public send API (game-thread side) --------
        // These return bool only for compatibility with existing callers; after
        // the threading split, "true" merely means "successfully enqueued".
        // The actual transmit happens later on the net thread.

        public bool SendPacket(int userId, byte[] source, Channel channelNo, PacketFlags flag = PacketFlags.RELIABLE)
        {
            if (0 <= userId && userId < _sender.Peers.Length)
            {
                _bridge.EnqueueOutbound(new OutboundUnicast(userId, source, channelNo, flag));
                return true;
            }
            return false;
        }

        public bool BroadcastPacket(byte[] data, Channel channelNo, PacketFlags flag = PacketFlags.RELIABLE)
        {
            if (data.Length >= 8)
            {
                // Fan out to one OutboundUnicast per configured slot. The net
                // thread skips entries whose peer slot is null (disconnected).
                // We iterate the player config (stable post-init) rather than
                // _peers[] because _peers is net-thread-owned.
                foreach (var ci in _playerManager.GetPlayers(false))
                {
                    _bridge.Outbound.Enqueue(new OutboundUnicast(ci.ClientId, data, channelNo, flag));
                }
                _bridge.OutboundSignal.Set();
                return true;
            }
            else
            {
                _bridge.EnqueueOutbound(new OutboundBroadcastRaw(data, channelNo, flag));
                return true;
            }
        }

        // TODO: find a way with no need of player manager
        public bool BroadcastPacketTeam(TeamId team, byte[] data, Channel channelNo,
            PacketFlags flag = PacketFlags.RELIABLE)
        {
            foreach (var ci in _playerManager.GetPlayers(false))
            {
                if (ci.Team == team)
                {
                    SendPacket(ci.ClientId, data, channelNo, flag);
                }
            }

            return true;
        }

        public bool BroadcastPacketVision(GameObject o, byte[] data, Channel channelNo,
            PacketFlags flag = PacketFlags.RELIABLE)
        {
            foreach (int pid in o.VisibleForPlayers)
            {
                SendPacket(pid, data, channelNo, flag);
            }
            return true;
        }

        // Sends to every player that has received a spawn packet for `o`, including players who
        // currently can't see it because they're in FoW. Use for "destroy"-style packets (FX_Kill,
        // OnDestroy, etc.) because without it, a player who lost vision before the destroy fires keeps
        // the object orphaned on their client until reconnect.
        public bool BroadcastPacketSpawned(GameObject o, byte[] data, Channel channelNo,
            PacketFlags flag = PacketFlags.RELIABLE)
        {
            foreach (int pid in o.SpawnedForPlayers)
            {
                SendPacket(pid, data, channelNo, flag);
            }
            return true;
        }

        public TeamId GetClientTeam(int userId)
        {
            var peerInfo = _playerManager.GetPeerInfo(userId);
            return peerInfo?.Team ?? TeamId.TEAM_NEUTRAL;
        }

        // -------- Net-thread receive path --------
        // HandleNetworkPacket is the entry point invoked by the network
        // thread's HostService loop. It does Blowfish decrypt + RequestConvertor
        // dispatch, then enqueues an InboundRequest for the game thread to
        // process at the start of its next tick. The handshake is the
        // exception: it stays synchronous because its replies must be sent on
        // the net thread (and we already are) before any subsequent packet
        // from this client is processed.
        public bool HandleNetworkPacket(Peer peer, Packet packet, Channel channelId)
        {
            var data = packet.Data;
            Console.WriteLine($"[PKT] channel={channelId} len={data.Length}");

            if (channelId == Channel.CHL_HANDSHAKE)
            {
                return HandleHandshake(peer, data);
            }

            if (data.Length >= 8)
            {
                int clientId = (peer.UserData != null ? (int)peer.UserData : 0) - 1;
                data = _sender.Blowfishes[clientId].Decrypt(data);
            }

            return ConvertAndEnqueue(peer, data, channelId);
        }

        private bool ConvertAndEnqueue(Peer peer, byte[] data, Channel channelId)
        {
            var reader = new BinaryReader(new MemoryStream(data));
            RequestConvertor convertor;

            if (channelId == Channel.CHL_COMMUNICATION || channelId == Channel.CHL_LOADING_SCREEN)
            {
                var loadScreenPacketId = (LoadScreenPacketID)reader.ReadByte();
                convertor = GetConvertor(loadScreenPacketId);
            }
            else
            {
                var gamePacketId = (GamePacketID)reader.ReadByte();
                convertor = GetConvertor(gamePacketId, channelId);
            }

            reader.Close();

            if (convertor != null)
            {
                int clientId = (peer.UserData != null ? (int)peer.UserData : 0) - 1;
                ICoreRequest req = convertor(data);
                _bridge.Inbound.Enqueue(new InboundRequest(clientId, req));
                return true;
            }

#if DEBUG
            PrintPacket(data, "Error: ");
#endif

            return false;
        }

        // Called from the game thread when an InboundRequest is dequeued at
        // the top of the game tick. Dispatches to the registered handlers.
        internal bool DispatchInboundRequest(int clientId, ICoreRequest req)
        {
            return _netReq.OnMessage(clientId, (dynamic)req);
        }

        // -------- Disconnect handling --------
        // Two entry points:
        //   1. NotifyDisconnectFromNet: called by the net thread's HostService
        //      loop when an EventType.DISCONNECT arrives. Nulls the peer slot
        //      (we own _peers[] here) and queues an InboundDisconnect for the
        //      game thread to process.
        //   2. ProcessDisconnectOnGameThread: called by the game thread, both
        //      when it dequeues an InboundDisconnect AND when HandleExit fires
        //      from the request stream. In the latter case the ENet peer is
        //      still alive, so we enqueue an OutboundClearPeer to release the
        //      slot once the net thread processes it.
        public bool NotifyDisconnectFromNet(Peer peer)
        {
            if (peer == null)
            {
                return true;
            }

            int clientId = (peer.UserData != null ? (int)peer.UserData : 0) - 1;
            if (clientId < 0)
            {
                // Didn't receive an ID by initiating a handshake.
                return true;
            }

            _sender.ClearPeer(clientId);
            _bridge.Inbound.Enqueue(new InboundDisconnect(clientId));
            return true;
        }

        public bool HandleDisconnect(int clientId)
        {
            // Public alias preserved for HandleExit. Same body as
            // ProcessDisconnectOnGameThread, plus a ClearPeer enqueue because
            // when this is called from the request stream the net thread has
            // not seen an ENet DISCONNECT yet.
            bool result = ProcessDisconnectOnGameThread(clientId);
            _bridge.EnqueueOutbound(new OutboundClearPeer(clientId));
            return result;
        }

        internal bool ProcessDisconnectOnGameThread(int clientId)
        {
            var peerInfo = _game.PlayerManager.GetPeerInfo(clientId);
            if (peerInfo == null || peerInfo.IsDisconnected)
            {
                Debug.WriteLine($"Prevented double disconnect of {peerInfo?.PlayerId}");
                return true;
            }

            Debug.WriteLine($"Player {peerInfo.PlayerId} disconnected!");

            var annoucement = new OnLeave { OtherNetID = peerInfo.Champion.NetId };
            _game.PacketNotifier.NotifyS2C_OnEventWorld(annoucement, peerInfo.Champion);
            peerInfo.IsDisconnected = true;
            peerInfo.IsStartedClient = false;
            peerInfo.ReconnectStartReady = false;
            peerInfo.ReconnectSpawnReady = false;

            return _game.CheckIfAllPlayersLeft() || peerInfo.Champion.OnDisconnect();
        }

        // -------- Handshake (net-thread, synchronous) --------
        // Runs on the net thread. peer.UserData and _peers[] are net-thread
        // owned, so we can read+write them directly. Replies are sent through
        // the sender directly (not via the bridge) so they land in-order
        // ahead of any queued packet for this client.
        private bool HandleHandshake(Peer peer, byte[] data)
        {
            var request = PacketReader.ReadKeyCheckRequest(data);

            var peerInfo = _playerManager.GetClientInfoByPlayerId(request.PlayerID);
            if (peerInfo == null)
            {
                Console.WriteLine($"[HANDSHAKE] Player ID {request.PlayerID} not found in config (players: {_playerManager.GetPlayers(false).Count})");
                return false;
            }

            if (_sender.Peers[peerInfo.ClientId] != null && !peerInfo.IsDisconnected)
            {
                Console.WriteLine($"[HANDSHAKE] Player {request.PlayerID} already connected");
                return false;
            }

            long playerID = _sender.Blowfishes[peerInfo.ClientId].Decrypt(request.CheckSum);
            if (request.PlayerID != playerID)
            {
                Console.WriteLine($"[HANDSHAKE] Blowfish fail: expected {request.PlayerID}, got {playerID}");
                return false;
            }

            peerInfo.IsStartedClient = true;

            Debug.WriteLine("Connected client No " + peerInfo.ClientId);

            peer.UserData = (int)peerInfo.ClientId + 1;
            _sender.Peers[peerInfo.ClientId] = peer;

            bool result = true;
            // inform players about their player numbers
            foreach (var player in _playerManager.GetPlayers(false))
            {
                var response = new KeyCheckPacket
                {
                    ClientID = player.ClientId,
                    PlayerID = player.PlayerId,
                    VersionNumber = request.VersionNo,
                    Action = 0,
                    CheckSum = request.CheckSum
                };
                // Direct send: we are on the net thread already and want the
                // handshake reply to leave before any queued packet for this
                // client is processed.
                result = _sender.SendUnicast(peerInfo.ClientId, response.GetBytes(), Channel.CHL_HANDSHAKE, PacketFlags.RELIABLE) && result;
            }

            return result;
        }
    }
}
