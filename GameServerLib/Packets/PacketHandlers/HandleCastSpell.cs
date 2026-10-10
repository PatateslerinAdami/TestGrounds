using GameServerCore.Enums;
using GameServerCore.Packets.Handlers;
using GameServerCore.Packets.PacketDefinitions.Requests;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleCastSpell : PacketHandlerBase<CastSpellRequest>
    {
        private readonly Game _game;
        private readonly NetworkIdManager _networkIdManager;
        private readonly PlayerManager _playerManager;

        public HandleCastSpell(Game game)
        {
            _game = game;
            _networkIdManager = game.NetworkIdManager;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, CastSpellRequest req)
        {
            var targetObj = _game.ObjectManager.GetObjectById(req.TargetNetID);
            var targetUnit = targetObj as AttackableUnit;
            var owner = _playerManager.GetPeerInfo(userId).Champion;
            if (owner == null)
            {
                return false;
            }

            if (!owner.Spells.TryGetValue(req.Slot, out var s) || s == null)
            {
                return false;
            }
            SpellCastInfo cs = new SpellCastInfo()
            {
                Position = req.Position,
                EndPosition = req.EndPosition,
                TargetUnit = targetUnit
            };
            var requestedItem = req.Slot >= (int)SpellSlotType.InventorySlots && req.Slot < (int)SpellSlotType.BluePillSlot
                ? owner.Inventory.GetItem((byte)(req.Slot - (byte)SpellSlotType.InventorySlots)) : null;
            
            cs.CouldCast = owner.TryCastRequestedSpell(s, req.Position, req.EndPosition, targetUnit, () =>
            {
                if (s.CastInfo.SpellSlot >= (int)SpellSlotType.InventorySlots && s.CastInfo.SpellSlot < (int)SpellSlotType.BluePillSlot)
                {
                    var item = owner.Inventory.GetItem((byte)(s.CastInfo.SpellSlot - (byte)SpellSlotType.InventorySlots));
                    if (item != null && item == requestedItem)
                    {
                        _game.PacketNotifier.NotifyUseItemAns(userId, owner, item);

                        if (item.ItemData.Consumed)
                        {
                            var inventory = owner.Inventory;
                            inventory.RemoveItem(inventory.GetItemSlot(item), owner);
                        }
                    }
                }
            });
            cs.WasQueued = owner.HasQueuedSpell(s);
            ApiEventManager.OnSpellPress.Publish(s, cs);
            return cs.CouldCast;
        }
    }
}
