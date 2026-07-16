using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace AIScripts
{
    // Shaco W "Jack in the Box" combat brain — the box's AiScript. In S1 the box has NO AI script at all:
    // it relies on the engine's default auto-attack AI and simply AUTO-ATTACKS the nearest enemy with its
    // Spell1, ShacoBoxSpell — which is literally tooltipped "JackintheBoxBasicAttack" (the box's basic
    // attack IS that spell, Attack1 animation, ApplyAttackDamage 0 → its TargetExecute/OnSpellHit applies
    // the magic damage). So we DON'T script a spell cast: we point the box's auto-attack at ShacoBoxSpell
    // (OverrideAutoAttacks) and let the shared AutoAttackComponent fire it in place at the box's attack
    // speed. The box is spawned Rooted, so it attacks where it stands and never chases.
    //
    // The fear pulse, the hidden/arming stealth + invulnerability, and the mana/lifetime death are owned by
    // the spell (JackInTheBox / W.cs) and the BoxTime → BoxFearAttack buff chain. This script only drives
    // combat once the box is ACTIVE, i.e. once an enemy has tripped its fear zone and BoxFearAttack is on
    // the box. While dormant (arming + hidden) it has no such buff, so it acquires nothing.
    public class ShacoBoxAI : BaseAIScript
    {
        private const float SCAN_INTERVAL = 0.15f;
        // The buff applied when the box triggers; its presence == "box is active and attacking".
        private const string ACTIVE_BUFF = "BoxFearAttack";

        protected override void OnActivateBehavior()
        {
            // The box owns its target selection — the engine must not auto-acquire for it. Firing is the
            // shared AutoAttackComponent's job once a target is set. Movement is impossible (the box is
            // spawned Rooted), so it only ever attacks in place.
            Owner.ScriptOwnsCombatSelection = true;
            // The box's auto-attack IS ShacoBoxSpell (its Spell1, tooltipped "JackintheBoxBasicAttack"):
            // override the engine's basic attack so every swing fires that spell, whose OnSpellHit applies
            // the magic damage. Matches S1, where the box auto-attacks with this spell and has no AI.
            OverrideAutoAttacks(Owner, true, "ShacoBoxSpell");
            InitTimer("TimerAcquire", SCAN_INTERVAL, true, TimerAcquire);
        }

        private void TimerAcquire()
        {
            if (Owner == null || Owner.IsDead || !Owner.HasBuff(ACTIVE_BUFF))
            {
                return;
            }

            float range = Owner.Stats.Range.Total;
            var current = Owner.TargetUnit;
            // Release a target that died or left attack range. The box is rooted (can't chase), so an
            // out-of-range target is simply dropped and re-acquired when something else enters range.
            if (current != null
                && (current.IsDead
                    || Vector2.DistanceSquared(Owner.Position, current.Position) > range * range))
            {
                Owner.SetTargetUnit(null, true);
                current = null;
            }

            if (current == null)
            {
                var next = FindTargetNear(Owner.Position, range);
                if (next != null)
                {
                    Owner.SetTargetUnit(next, true);
                }
            }
            // Firing the swing (= the ShacoBoxSpell auto-attack) is the shared AutoAttackComponent's job
            // once TargetUnit is set; the engine paces it at the box's attack speed.
        }
    }
}
