using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleSwapItems : PacketHandlerBase<SwapItemsRequest>
    {
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public HandleSwapItems(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, SwapItemsRequest req)
        {
            if (req.Source >= 6 || req.Destination >= 6 || req.Source == req.Destination)
            {
                return false;
            }

            var champion = _playerManager.GetPeerInfo(userId).Champion;

            var sourceSpellSlot = (byte)(req.Source + Shop.ITEM_ACTIVE_OFFSET);
            var destinationSpellSlot = (byte)(req.Destination + Shop.ITEM_ACTIVE_OFFSET);
            champion.Inventory.SwapItems(req.Source, req.Destination);

            champion.SwapSpells(sourceSpellSlot, destinationSpellSlot, networked: false);
            _game.PacketNotifier.NotifySwapItemAns(champion, req.Source, req.Destination);

            foreach (var slot in new[] { sourceSpellSlot, destinationSpellSlot })
            {
                var spell = champion.Spells[slot];
                _game.PacketNotifier.NotifyCHAR_SetCooldown(champion, slot, spell.CurrentCooldown,
                    spell.GetCooldown(), userId);
            }
            return true;
        }
    }
}
