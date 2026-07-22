using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;
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

                // The R-press guide is MOVE-ONLY (Riot-faithful). Evidence: Annie's guide spell
                // PetCommandParticle.lua is pure metadata with NO command/target-detection logic (unlike
                // ViktorChaosStormGuide, which DOES proximity-target via BuildingBlocks); the client shows
                // only the waypoint cursor; the cast carries no TargetNetID (verified live: targetNetID=0
                // even when clicking directly on an enemy); and S1 had no Annie guide spell at all. So the
                // guide just issues a hard move to the clicked point. Tibbers still attacks when steered
                // onto an enemy — via the pet's OWN AI: on arrival it enters AI_PET_HARDIDLE and
                // TimerFindEnemies auto-acquires any enemy in acquisition range. Explicit attack-on-target
                // is the separate pet command (PetHardAttack, the alt-click pet bar), not the guide.
                //
                // Route through the pet's brain (PetAI.OnOrder), not its MoveOrder/waypoints directly:
                // poking those bypasses the AI's _aiState, so its 0.15s timers (on a stale IDLE state)
                // would immediately override the move with auto-return/acquire ("pet only moves while I
                // spam the control"). A hard state (AI_PET_HARDMOVE) is respected by the timers.
                Pet.IssueOrder(OrderType.PetHardMove, end, null);
            }
        }
    }
}
