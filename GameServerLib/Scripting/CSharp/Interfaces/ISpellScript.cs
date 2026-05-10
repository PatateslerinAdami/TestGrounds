using GameServerCore.Enums;
using System.Numerics;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace GameServerCore.Scripting.CSharp
{
    public interface ISpellScript
    {
        SpellScriptMetadata ScriptMetadata { get; }

        void OnActivate(ObjAIBase owner, Spell spell)
        {
        }

        void OnPostActivate(ObjAIBase owner, Spell spell)
        {
        }

        void OnDeactivate(ObjAIBase owner, Spell spell)
        {
        }

        void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
        }

        void OnSpellCast(Spell spell)
        {
        }

        void OnSpellPostCast(Spell spell)
        {
        }

        void OnSpellChannel(Spell spell)
        {
        }

        void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
        }

        void OnSpellPostChannel(Spell spell)
        {
        }

        // Fires every server tick while channeling. Channel-entry call passes diff=0f (synchronous
        // with OnSpellChannel). Scripts gate per-spell rate via PeriodicTicker.ConsumeTicks(diff, periodMs, ...).
        void OnSpellChannelUpdate(Spell spell, float diff)
        {
        }

        // Fires when the client sends C2S_SpellChargeUpdateReq during a channel e.g. player adjusting
        // aim/charge target. NOT time-driven (use OnSpellChannelUpdate for that). `forceStop`=true
        // signals button release; engine cancels the channel right after this hook returns.
        void OnSpellChargeUpdate(Spell spell, Vector3 position, bool forceStop)
        {
        }
        void OnSpellEvolve(Spell spell)
        {
        }
        void OnUpdate(float diff)
        {
        }
    }
}
