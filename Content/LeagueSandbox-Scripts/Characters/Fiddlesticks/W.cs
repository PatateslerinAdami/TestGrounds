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
    // DATA SOURCES (both are 4.20):
    //   • Per-tick damage  → DrainChannel.json (the channel owns its damage, as in S1): Effect1
    //     (50/75/100/130/160) as damage-PER-SECOND + Coefficient (0.5) AP-PER-SECOND. Per 0.5s tick
    //     = value × 0.5. ⚠ ASSUMPTION: Effect1 is per-second (matches S1's per-second 60/90/… shape
    //     and keeps channel totals sane). Drain.json also carries an S1-identical set (Effect1
    //     60/90/120/150/180, Coef 0.45) — if a replay shows the launcher values are canonical, swap
    //     `_spell` → `_parentSpell` below. Pin the exact per-second-vs-per-tick + tick rate by replay.
    //   • Heal %           → Drain.json (launcher) Effect3 (60/65/70/75/80 → ÷100 = 0.60‥0.80).
    //     DrainChannel.json Effect3 is all-zero, so the drain % lives on the W slot, as in S1.
    //   • Leash 650        → DrainChannel.json CastRange (== S1's 650 distance gate).
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
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };

        public void OnSpellCast(Spell spell)
        {
            spell.CastInfo.Owner.StopMovement(networked: false);
            var target = spell.CastInfo.Targets.FirstOrDefault()?.Unit;
            if (target != null)
            {
                // S1 Drain.TargetExecute → force-cast DrainChannel (ExtraSlot 0) on the target.
                SpellCast(spell.CastInfo.Owner, 0, SpellSlotType.ExtraSlots, false, target, target.Position);
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

        private const float TICK_INTERVAL_MS = 500f;    // S1 TimeBetweenExecutions = 0.5
        private const float TICK_SECONDS     = 0.5f;    // per-second value → per-tick multiplier
        private const float LEASH_RANGE_SQR  = 650f * 650f;

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

            // S1 ChannelingStart: target debuff marker + owner heal marker + tether particle.
            AddBuff("Drain", 5.0f, 1, _parentSpell ?? spell, _target, _owner);
            AddBuff("Fearmonger_marker", 5.0f, 1, _parentSpell ?? spell, _owner, _owner);
            _tetherParticle = AddParticleTarget(_owner, _owner, "Drain", _target, 5.0f, bone: "spine", targetBone: "spine");

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
                executeImmediately: false, DealTick);
        }

        private void DealTick()
        {
            var level = (_parentSpell ?? _spell).CastInfo.SpellLevel;
            if (level <= 0)
            {
                return;
            }

            // Damage from the channel spell's own SpellData (DrainChannel.json), per-second → per-tick.
            var ap = _owner.Stats.AbilityPower.Total;
            var perSecond = _spell.SpellData.EffectLevelAmount[1][level] + ap * _spell.SpellData.Coefficient;
            var perTick = perSecond * TICK_SECONDS;

            // Heal % from the W-slot SpellData (Drain.json Effect3 = 60‥80 → ÷100).
            var healPercent = (_parentSpell ?? _spell).SpellData.EffectLevelAmount[3][level] / 100f;

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
            // S1 ChannelingSuccess/CancelStop: remove Drain (target) + Fearmonger_marker (owner) + particle.
            if (_target != null)
            {
                RemoveBuff(_target, "Drain");
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
