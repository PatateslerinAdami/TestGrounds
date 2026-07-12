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

        // === CHARGE pipeline (UseChargeChanneling=1 spells, e.g. Varus Q) ===
        // For charge-style spells the engine fires these INSTEAD of the OnSpellChannel* family.
        // Routing: Spell pipeline checks SpellData.UseChargeChanneling and publishes the
        // charge-specific event when true.

        // Charge begins (analogous to OnSpellChannel). Particle/buff/animation setup goes here.
        void OnSpellChargeStart(Spell spell)
        {
        }

        // Per-server-tick during charge (analogous to OnSpellChannelUpdate). Charge-entry call
        // passes diff=0f synchronous with OnSpellChargeStart. Scripts use this for periodic FX
        // or charge-progress reactions.
        void OnSpellChargeTick(Spell spell, float diff)
        {
        }

        // Charge complete — player released OR max charge time elapsed. Missile fire goes here.
        // Engine handles the wire-side fire (re-broadcast NPC_CastSpellAns with IsContinuationCast=true) so
        // the script's job is just spawning the actual missile + cleanup.
        void OnSpellChargeFire(Spell spell)
        {
        }

        // Real interrupt (stun/silence/death/casting-another-spell). NOT called on normal fire-release;
        // the release-path goes through OnSpellChargeFire instead. Scripts use this for charge-cleanup
        // (remove charge particles, refund mana, etc).
        void OnSpellChargeCancel(Spell spell, ChannelingStopSource reason)
        {
        }

        // Fires when the client sends C2S_SpellChargeUpdateReq during a channel e.g. player adjusting
        // aim/charge target. NOT time-driven (use OnSpellChargeTick for that). `forceStop`=true
        // signals button release; engine fires OnSpellChargeFire right after this hook returns.
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
