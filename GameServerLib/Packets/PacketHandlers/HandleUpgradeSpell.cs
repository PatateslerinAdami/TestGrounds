using GameServerCore.Packets.Handlers;
using GameServerCore.Packets.PacketDefinitions.Requests;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleUpgradeSpellReq : PacketHandlerBase<UpgradeSpellReq>
    {
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public HandleUpgradeSpellReq(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, UpgradeSpellReq req)
        {
            // TODO: Check if can up skill
            // TODO: Implement usage of req.IsEvolve

            var champion = _playerManager.GetPeerInfo(userId).Champion;
            if (req.IsEvolve)
            {
                if (champion.Stats.EvolvePoints <= 0)
                {
                    return false;
                }

                champion.Stats.EvolvePoints--;
                champion.Stats.EvolveFlags |= (uint)(1 << req.Slot);

                if (champion.Spells.TryGetValue(req.Slot, out var spell))
                {
                    spell.Script.OnSpellEvolve(spell);
                }

                LeaguePackets.Game.Events.IEvent evolveEvent = null;
                switch (req.Slot)
                {
                    case 0: evolveEvent = new LeaguePackets.Game.Events.OnSpellEvolve1(); break;
                    case 1: evolveEvent = new LeaguePackets.Game.Events.OnSpellEvolve2(); break;
                    case 2: evolveEvent = new LeaguePackets.Game.Events.OnSpellEvolve3(); break;
                    case 3: evolveEvent = new LeaguePackets.Game.Events.OnSpellEvolve4(); break;
                }

                if (evolveEvent != null)
                {
                    _game.PacketNotifier.NotifyOnEvent(evolveEvent, champion);
                }

                return true;
            }
            else
            {
                var s = champion.LevelUpSpell(req.Slot);
                if (s == null)
                {
                    return false;
                }

                _game.PacketNotifier.NotifyNPC_UpgradeSpellAns(userId, champion.NetId, req.Slot, s.CastInfo.SpellLevel, champion.SkillPoints);
                champion.Stats.SetSpellEnabled(req.Slot, true);

                return true;
            }
        }
    }
}
