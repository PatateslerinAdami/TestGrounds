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

                // Route the pet command through the pet's brain (PetAI.OnOrder) instead of poking its
                // MoveOrder/waypoints directly. Setting MoveOrder + SetWaypoints here bypassed the AI's
                // _aiState, so the AI's 0.15s timers (running on a stale IDLE state) immediately
                // overrode the move with their auto-return/acquire — the "pet only moves while I spam
                // the control" bug. IssueOrder makes the AI set AI_PET_HARDMOVE/HARDATTACK, which its
                // timers respect (hard states neither auto-return nor re-acquire).
                var unitsInRange =
                    EnumerateValidUnitsInRange(spell.CastInfo.Owner, end, 100.0f, true, SpellDataFlags.AffectEnemies)
                        .ToList();
                if (unitsInRange.Count > 0)
                {
                    Pet.IssueOrder(OrderType.PetHardAttack, unitsInRange[0], end);
                    for (int i = 0; i < unitsInRange.Count; i++)
                    {
                        spell.CastInfo.SetTarget(unitsInRange[i], i);
                    }
                }
                else
                {
                    Pet.IssueOrder(OrderType.PetHardMove, null, end);
                }
            }
        }
    }
}
