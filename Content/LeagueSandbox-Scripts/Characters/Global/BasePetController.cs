using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class BasePetController : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            NotSingleTargetSpell = true,
            DoesntBreakShields = true,
            TriggersSpellCasts = false,
            IsPetDurationBuff = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var pet = owner.GetPet();
            if (pet == null || owner.IsDead || !float.IsFinite(end.X) || !float.IsFinite(end.Y))
            {
                spell.CancelCast();
                return;
            }

            if (target == owner) target = null;
            if (target == null)
            {
                target = GetUnitsInRange(pet, end, 100, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral
                    | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectTurrets
                    | SpellDataFlags.AffectBuildings).FirstOrDefault(unit => TeamHasVision(pet.Team, unit));
            }
            var order = target != null ? OrderType.PetHardAttack : OrderType.PetHardMove;
            if (!pet.IssueCommand(order, end, target)) spell.CancelCast();
        }
    }
}
