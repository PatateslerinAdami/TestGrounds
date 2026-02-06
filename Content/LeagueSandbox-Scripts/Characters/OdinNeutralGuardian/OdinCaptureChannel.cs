
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class OdinCaptureChannel : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        public void OnSpellCast(Spell spell)
        {
        }
        public void OnSpellChannel(Spell spell)
        {
        }
        public void OnSpellPostCast(Spell spell)
        {
        }
        public void OnSpellPostChannel(Spell spell)
        {
        }
        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
        }
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
        }
    }
}
