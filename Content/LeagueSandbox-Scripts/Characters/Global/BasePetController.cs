using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace Spells
{
    public class BasePetController : ISpellScript
    {
        private Pet Pet;

        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            DoesntBreakShields = true,
            TriggersSpellCasts = false,
            IsDamagingSpell = true,
            SpellDamageRatio = 0.5f,
            IsPetDurationBuff = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            Pet = owner.GetPet();
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (Pet != null)
            {
                // likely AddBuff("PetCommandParticle") here (refer to preload for particles)
                //This particle get's displayed globably
                AddParticle(owner, null, "cursor_moveto", start);

                // Prefer the actual cast target: when the player hovers an enemy with the guide, the
                // client passes the hovered unit through `target`, so attack THAT. Only when nothing was
                // hovered do we fall back to a proximity grab of the nearest enemy within the spell's
                // CastRadius of the point. The old code skipped `target` and used a hardcoded 100u radius
                // (far smaller than the ~250 CastRadius), so hovering an enemy whose center sat >100u
                // from the click point fell through to a plain move ("pet walks to the position instead
                // of attacking the target").
                AttackableUnit attackTarget = null;
                if (target != null && target.Team != owner.Team)
                {
                    attackTarget = target;
                }
                else
                {
                    float radius = (spell.SpellData.CastRadius != null && spell.SpellData.CastRadius[0] > 0f)
                        ? spell.SpellData.CastRadius[0]
                        : 250.0f;
                    attackTarget = EnumerateValidUnitsInRange(spell.CastInfo.Owner, end, radius, true,
                            SpellDataFlags.AffectEnemies)
                        .OrderBy(u => Vector2.DistanceSquared(u.Position, end))
                        .FirstOrDefault();
                }

                // Route the pet command through the pet's brain (PetAI.OnOrder) instead of poking its
                // MoveOrder/waypoints directly. Setting MoveOrder + SetWaypoints here bypassed the AI's
                // _aiState, so the AI's 0.15s timers (running on a stale IDLE state) immediately
                // overrode the move with their auto-return/acquire — the "pet only moves while I spam
                // the control" bug. IssueOrder makes the AI set AI_PET_HARDMOVE/HARDATTACK, which its
                // timers respect (hard states neither auto-return nor re-acquire).
                if (attackTarget != null)
                {
                    Pet.IssueOrder(OrderType.PetHardAttack, attackTarget, end);
                    spell.CastInfo.SetTarget(attackTarget, 0);
                }
                else
                {
                    Pet.IssueOrder(OrderType.PetHardMove, null, end);
                }
            }
        }
    }
}
