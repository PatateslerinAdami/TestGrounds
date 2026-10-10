using System.Numerics;
using ShacoScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class Hallucinate : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            TriggersSpellCasts = true,
            CastTime = 0.5f,
            CastingBreaksStealth = true,
            IsPetDurationBuff = true,
            NotSingleTargetSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            spell.SpellData.Flags &= ~SpellDataFlags.InstantCast;

            spell.CastInfo.SpellHash = GameServerCore.Content.HashFunctions.HashString("HallucinateFull");
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            AddParticle(owner, null, "HallucinatePoof", owner.Position, lifetime: 1);
            AddBuff("HallucinateVanish", 0.5f, 1, spell, owner, owner);
        }

        public void OnSpellPostCast(Spell spell)
        {
            if (spell.CastInfo.Owner is not Champion owner || owner.IsDead) return;
            RemoveBuff(owner, "HallucinateVanish");
            owner.RefreshBuffStatus();
            NotifyUnitStats(owner, reliable: true);
            var facing = new Vector2(owner.Direction.X, owner.Direction.Z);
            if (facing.LengthSquared() < 0.001f) facing = Vector2.UnitX;
            facing = Vector2.Normalize(facing);
            var clonePosition = GetClosestTerrainExit(owner.Position - facing * 75, owner.PathfindingRadius);
            owner.TeleportTo(owner.Position + facing * 75);
            var clone = CreateClonePet(owner, spell, owner, clonePosition, "Hallucinate", 18,
                stats: ShacoHelpers.CloneStats(owner), AIScript: "ShacoClone");
            FaceDirection(clone.Position + facing, clone);
            clone.SetReturnRadius(200);
            AddBuff("Hallucinate", 18, 1, spell, owner, owner);
            AddParticle(owner, null, "HallucinatePoof", owner.Position, lifetime: 1);
        }
    }

    public class HallucinateFull : Hallucinate 
    { 
    }
    public class HallucinateGuide : BasePetController 
    { 
    }
}
