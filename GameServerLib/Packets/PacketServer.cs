using LENet;
using GameServerCore.Packets.Handlers;
using GameServerCore.Packets.PacketDefinitions;
using System;
using System.Threading;
using Channel = GameServerCore.Packets.Enums.Channel;
using Version = LENet.Version;
using LeagueSandbox.GameServer;

namespace PacketDefinitions420
{
    /// <summary>
    /// Class responsible for the server's networking of the game.
    /// Owns the dedicated I/O thread which polls ENet, decrypts and dispatches
    /// inbound packets onto the game thread, and drains the outbound command
    /// queue.
    /// </summary>
    public class PacketServer
    {
        private Host _server;
        private readonly uint _serverHost = Address.Any;
        private Game _game;
        protected const int PEER_MTU = 996;
        // Polling cadence used when the socket is idle. Short enough that newly
        // enqueued outbound commands are flushed promptly even without the
        // OutboundSignal pulse, long enough to avoid burning CPU when the
        // server is genuinely quiet.
        private const uint NET_POLL_TIMEOUT_MS = 5;

        private NetworkSender _sender;
        public NetworkBridge Bridge { get; private set; }

        public PacketHandlerManager PacketHandlerManager { get; private set; }

        private Thread _netThread;

        public void InitServer(ushort port, string[] blowfishKeys, Game game, NetworkHandler<ICoreRequest> netReq, NetworkHandler<ICoreRequest> netResp)
        {
            _game = game;
            _server = new Host(Version.Patch420, new Address(_serverHost, port), 32, 32, 0, 0);

            BlowFish[] blowfishes = new BlowFish[blowfishKeys.Length];
            for(int i = 0; i < blowfishKeys.Length; i++)
            {
                var key = Convert.FromBase64String(blowfishKeys[i]);
                if (key.Length <= 0)
                {
                    throw new Exception($"Invalid blowfish key supplied({ key })");
                }
                blowfishes[i] = new BlowFish(key);
            }

            var peers = new Peer[blowfishKeys.Length];
            _sender = new NetworkSender(peers, blowfishes, _server);
            Bridge = new NetworkBridge();

            PacketHandlerManager = new PacketHandlerManager(_sender, Bridge, game, netReq, netResp);
        }

        // Spawns the dedicated I/O thread. The game thread continues into
        // Game.GameLoop() while this thread runs HostService + outbound drain
        // until Bridge.Stop is set.
        public void StartNetThread()
        {
            _netThread = new Thread(NetThreadMain)
            {
                Name = "ENet I/O",
                IsBackground = true
            };
            _netThread.Start();
        }

        public void StopNetThread()
        {
            if (Bridge != null)
            {
                Bridge.Stop = true;
                Bridge.OutboundSignal.Set();
            }
            _netThread?.Join();
        }

        private void NetThreadMain()
        {
            var ev = new Event();
            while (!Bridge.Stop)
            {
                // Drain everything pending on the outbound queue first. This
                // is the hot path: most ticks the game thread enqueues several
                // commands and we want them out the door before we go to
                // sleep on HostService.
                while (Bridge.Outbound.TryDequeue(out var cmd))
                {
                    try
                    {
                        _sender.Execute(cmd);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($"[NetThread] Outbound execute failed: {e}");
                    }
                }

                int rc = _server.HostService(ev, NET_POLL_TIMEOUT_MS);
                if (rc < 0)
                {
                    // ENet error. Pause briefly to avoid a busy loop on a
                    // persistent socket failure.
                    Thread.Sleep(1);
                    continue;
                }

                // Drain all events that arrived during the poll without
                // blocking again.
                do
                {
                    DispatchEnetEvent(ev);
                }
                while (_server.HostService(ev, 0) > 0);

                // If both queues were empty and HostService returned 0, sit on
                // the wait handle so we don't spin. The game thread sets this
                // any time it enqueues an outbound command.
                if (Bridge.Outbound.IsEmpty)
                {
                    Bridge.OutboundSignal.WaitOne((int)NET_POLL_TIMEOUT_MS);
                }
            }
        }

        private void DispatchEnetEvent(Event enetEvent)
        {
            switch (enetEvent.Type)
            {
                case EventType.CONNECT:
                    {
                        // Set some defaults
                        enetEvent.Peer.MTU = PEER_MTU;
                        enetEvent.Data = 0;
                    }
                    break;
                case EventType.RECEIVE:
                    {
                        var channel = (Channel)enetEvent.ChannelID;
                        PacketHandlerManager.HandleNetworkPacket(enetEvent.Peer, enetEvent.Packet, channel);
                    }
                    break;
                case EventType.DISCONNECT:
                    {
                        PacketHandlerManager.NotifyDisconnectFromNet(enetEvent.Peer);
                    }
                    break;
            }
        }
    }
}
