using GameServerCore.Enums;
using LeaguePackets;
using LeaguePackets.Common;
using LeaguePackets.Game;
using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace PacketDefinitions420
{
    internal static class InstantCastPresentation
    {
        public static bool NeedsCarrier(Game? game, Spell spell, NPC_CastSpellAns packet)
        {
            if (game == null || packet.CastInfo.IsAutoAttack)
            {
                return false;
            }

            var pending = spell.CastInfo.Owner.GetCastSpell();
            if (pending == null || pending == spell)
            {
                return false;
            }

            if (pending.State != SpellState.STATE_CASTING
                || pending.CurrentCastTime <= 0
                || pending.CastInfo.IsAutoAttack)
            {
                return false;
            }

            var content = game.Config?.ContentManager;
            if (content == null)
            {
                return false;
            }

            var incomingBehavior = content.GetLoadedSpellCastBehavior(packet.CastInfo.SpellHash);
            uint pendingHash = pending.CastInfo.SpellHash != 0
                ? pending.CastInfo.SpellHash
                : (uint)pending.GetId();
            var pendingBehavior = content.GetLoadedSpellCastBehavior(pendingHash);
            if (incomingBehavior == null || pendingBehavior == null)
            {
                return false;
            }

            if (!incomingBehavior.InstantCast
                || incomingBehavior.BelongsToAvatar
                || incomingBehavior.DoesntBreakChannels)
            {
                return false;
            }

            if (incomingBehavior.IsToggleSpell)
            {
                return false;
            }

            if (pendingBehavior.InstantCast || pendingBehavior.BelongsToAvatar)
            {
                return false;
            }

            if (UsesFireOnlyRoute(
                packet.CastInfo.SpellSlot,
                packet.CastInfo.IsForceCastingOrChannel))
            {
                return false;
            }

            return true;
        }

        private static bool UsesFireOnlyRoute(byte slot, bool forced)
        {
            return !forced && slot >= 45 && slot != 62;
        }

        public static IReadOnlyList<byte[]> Create(Game game, Spell spell, NPC_CastSpellAns packet)
        {
            uint helperId = game.NetworkIdManager.GetNewNetId();
            var owner = spell.CastInfo.Owner;
            var spawn = new SpawnMinionS2C
            {
                SenderNetID = helperId,
                NetID = helperId,
                OwnerNetID = owner.NetId,
                NetNodeID = (byte)NetNodeID.Spawned,
                Position = packet.CastInfo.SpellCastLaunchPosition,
                TeamID = (ushort)owner.Team,
                IgnoreCollision = true,
                IsTargetable = false,
                IsTargetableToTeamSpellFlags = 0,
                VisibilitySize = 0,
                Name = "InstantCastPresentation",
                SkinName = "OdinTestCubeRender",
                InitialLevel = 1
            };
            var hideHealthBar = new S2C_ShowHealthBar
            {
                SenderNetID = helperId,
                ShowHealthBar = false
            };

            packet.SenderNetID = helperId;
            packet.CastInfo.IsOverrideCastPosition = true;
            byte[] castBytes = packet.GetBytes();
            var cleanup = new NPC_ForceDead
            {
                SenderNetID = helperId,
                DeathDuration = 0
            };
            var batch = new Batched
            {
                Packets = new List<GamePacket>
                {
                    spawn,
                    hideHealthBar
                }
            };
            var packets = new List<byte[]>();
            if (castBytes.Length <= 260)
            {
                batch.Packets.Add(packet);
                batch.Packets.Add(cleanup);
                packets.Add(batch.GetBytes());
            }
            else
            {
                packets.Add(batch.GetBytes());
                packets.Add(castBytes);
                packets.Add(cleanup.GetBytes());
            }

            return packets;
        }
    }
}
