using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    // ── Fiddlesticks W (Drain) — faithful L2 port of the S1 BB scripts (Drain / DrainChannel /
    //    DrainCheck / Fearmonger_marker) with patch-4.20 data. ──────────────────────────────────
    //
    // S1 flow: Drain (the W slot) validates the target and force-casts DrainChannel (ExtraSlot 0).
    // DrainChannel channels for 5s, dealing magic damage on start and every 0.5s, healing Fiddle for
    // a % of the damage, and cancels if the target dies / leaves 650 range / the owner dies.
    //
    // DATA SOURCES — all from the W-slot Drain.json (the 4.20-authoritative source). DrainChannel.json
    // is S1-STALE (CastRange 650 = old tether, Effect1 50/75/… ≠ 4.20) — do NOT read from it.
    //   • Damage/sec → Drain.json Effect1 (60/90/120/150/180) + Coefficient (0.45) AP. Per tick =
    //     value × TICK_SECONDS (0.25s in 4.20). Per-second confirmed by patch 4.15 ("refresh rate
    //     0.5s → 0.25s", no damage change → only holds if the value is per-second: total DPS constant).
    //   • Heal %     → Drain.json Effect3 (60/65/70/75/80 → ÷100 = 0.60‥0.80).
    //   • Leash 800  → patch 4.15 ("tether 750 → 800"); S1 lua / DrainChannel.json say 650 (S1-era).
    //
    // OMITTED vs S1 (documented, not silently dropped):
    //   • DrainCheck (empty marker buff) — S1 uses it purely to test target-buffability before
    //     casting the channel; our normal SpellCast target validation covers that gate.
    //   • GlobalDrain buff — S1's heal vehicle (heals Owner for DrainPercent × damage). Folded into
    //     a direct TakeHeal(HealType.Drain) per tick here.
    public class Drain : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new()
        {
            // 4.20 Drain stub: CastingBreaksStealth=true, DoesntBreakShields=false (shield blocks the
            // tether — handled at DrainChannel start via BreakSpellShields), IsDamagingSpell=false
            // (damage lives in DrainChannel). NOTE: the stub also has DoesntTriggerSpellCasts=true,
            // but our engine ties the OnSpellCast SCRIPT HOOK to TriggersSpellCasts (Spell.cs:208) —
            // setting it false would stop OnSpellCast firing and break the launch. Engine conflation:
            // "fire OnSpellCast hook" vs "proc on-cast reactions" aren't separable, so we keep it true.
            TriggersSpellCasts = true,
            CastingBreaksStealth = true,
            AutoFaceDirection = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            
        }

        public void OnSpellCast(Spell spell)
        {
            spell.CastInfo.Owner.StopMovement(networked: false);
            var target = spell.CastInfo.Targets.FirstOrDefault()?.Unit;
            if (target != null)
            {
                // S1 Drain.TargetExecute → force-cast DrainChannel (ExtraSlot 0) on the target.
                SpellCast(spell.CastInfo.Owner, 0, SpellSlotType.ExtraSlots, false, target, target.Position,  true, spell.CastInfo.SpellLevel);
            }
        }
    }

    public class DrainChannel : ISpellScript
    {
        private ObjAIBase _owner;
        private AttackableUnit _target;
        private Spell _spell;
        private Spell _parentSpell;
        private Particle _tetherParticle;

        // Patch 4.15: refresh rate increased to 0.25s (from the S1-era 0.5s in DrainChannel.lua).
        // We target 4.20, so 0.25s. Per-tick damage = per-second × TICK_SECONDS keeps total DPS
        // constant across this refresh-rate change (exactly what the 4.15 note implies — which also
        // corroborates that Effect1/Coefficient are per-SECOND values).
        private const float TICK_INTERVAL_MS = 250f;    // 4.15+ TimeBetweenExecutions = 0.25
        private const float TICK_SECONDS     = 0.25f;   // per-second value → per-tick multiplier
        // Patch 4.15: tether range increased to 800 (from 750). We target 4.20 → 800.
        // ⚠ NOTE: S1 DrainChannel.lua's distance gate AND DrainChannel.json CastRange are both 650 —
        // the S1-era value. That the repo JSON still carries 650 means its Drain data is (at least
        // partly) S1-stale, NOT 4.20-authoritative → the damage values read from it are also suspect
        // and should be cross-checked against 4.20 patch notes / a true 4.20 data source.
        private const float LEASH_RANGE_SQR  = 800f * 800f;

        public SpellScriptMetadata ScriptMetadata => new()
        {
            CastingBreaksStealth = true,
            IsDamagingSpell = true,
            ChannelDuration = 5.0f,       // S1 DrainChannel ChannelDuration = 5
            DoesntBreakShields = true,    // S1 DrainChannel DoesntBreakShields = true (persistent DoT)
            TriggersSpellCasts = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _owner = owner;
            _target = target;
            _spell = spell;
            _parentSpell = owner.GetSpell("Drain");
        }

        public void OnSpellChannel(Spell spell)
        {
            if (_owner == null || _target == null)
            {
                return;
            }

            // 4.20 (Drain stub DoesntBreakShields=false): a spell shield BLOCKS applying the tether —
            // consume it and abort before anything is applied. The ongoing ticks below use direct
            // TakeDamage (DrainChannel DoesntBreakShields=true), so an already-applied tether keeps
            // damaging through a shield gained later. BreakSpellShields returns false = shield ate it.
            if (!BreakSpellShields(_target, _parentSpell ?? spell))
            {
                spell.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
                return;
            }

            // ChannelingStart: target debuff marker + owner heal marker + tether particle.
            // Replay-verified 4.20 (096729f… ARAM): the target buff is "DrainChannel" (DAMAGE), NOT
            // "Drain" as in the S1 lua (S1→4.20 buff rename). Fearmonger_marker (HEAL) unchanged.
            AddBuff("DrainChannel", 5.0f, 1, _parentSpell ?? spell, _target, _owner);
            AddBuff("Fearmonger_marker", 5.0f, 1, _parentSpell ?? spell, _owner, _owner);
            _tetherParticle = SpellEffectCreate("Drain.troy",_owner, _owner,  _target, lifetime: 5.0f, boneName: "C_Buffbone_Glb_Chest_Loc", targetBoneName: "C_Buffbone_Glb_Chest_Loc", flags: FXFlags.SimulateWhileOffScreen);

            // S1 sets DrainExecuted = GetTime() in ChannelingStart (the periodic tick anchor), then
            // deals one immediate tick. Pre-seeding the anchor makes the periodic below fire its
            // first tick one full interval (0.5s) later — ExecuteImmediately = false, as in S1.
            _spell.CastInfo.InstanceVars.Set("DrainExecuted", ApiMapFunctionManager.GameTime());
            DealTick();
        }

        public void OnSpellChannelUpdate(Spell spell, float diff)
        {
            if (_spell == null || _owner.ChannelSpell != _spell)
            {
                return;
            }

            // S1 ChannelingUpdate guards: target dead / out of 650 range / owner dead → cancel.
            if (_target == null || _target.IsDead || _owner.IsDead
                || Vector2.DistanceSquared(_owner.Position, _target.Position) >= LEASH_RANGE_SQR)
            {
                _spell.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
                return;
            }

            // S1 BBExecutePeriodically(TimeBetweenExecutions=0.5, TrackTimeVar="DrainExecuted",
            // TrackTimeVarTable="InstanceVars", ExecuteImmediately=false).
            ExecutePeriodically(_spell.CastInfo.InstanceVars, "DrainExecuted", TICK_INTERVAL_MS,
                executeImmediately: false, maxTicks: 0, DealTick);
        }

        private void DealTick()
        {
            // All values come from the W-slot SpellData (Drain.json) — the 4.20-authoritative source.
            // (DrainChannel.json is S1-stale: CastRange 650 = old tether, Effect1 50/75/… ≠ 4.20.)
            var data = _parentSpell ?? _spell;
            var level = data.CastInfo.SpellLevel;
            if (level <= 0)
            {
                return;
            }

            // Damage-per-second → per-tick. Drain.json Effect1 (60/90/120/150/180) + Coefficient 0.45.
            var ap = _owner.Stats.AbilityPower.Total;
            var perSecond = data.SpellData.EffectLevelAmount[1][level] + ap * data.SpellData.Coefficient;
            var perTick = perSecond * TICK_SECONDS;

            // Heal % from Drain.json Effect3 (60/65/70/75/80 → ÷100 = 0.60‥0.80).
            var healPercent = data.SpellData.EffectLevelAmount[3][level] / 100f;

            // S1 SourceDamageType = DAMAGESOURCE_SPELLPERSIST → our DAMAGE_SOURCE_PERIODIC.
            _target.TakeDamage(_owner, perTick, DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_PERIODIC, DamageResultType.RESULT_NORMAL);
            _owner.TakeHeal(_owner, perTick * healPercent, HealType.Drain);
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            Cleanup();
        }

        public void OnSpellPostChannel(Spell spell)
        {
            Cleanup();
        }

        private void Cleanup()
        {
            // ChannelingSuccess/CancelStop: remove DrainChannel (target) + Fearmonger_marker (owner) + particle.
            if (_target != null)
            {
                RemoveBuff(_target, "DrainChannel");
            }
            if (_owner != null)
            {
                RemoveBuff(_owner, "Fearmonger_marker");
            }
            if (_tetherParticle != null)
            {
                RemoveParticle(_tetherParticle);
                _tetherParticle = null;
            }
        }
    }
}
