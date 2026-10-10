using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class Deceive : ISpellScript
    {
        private Vector2 _destination;
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!float.IsFinite(end.X) || !float.IsFinite(end.Y))
            {
                spell.CancelCast();
                return;
            }

            var offset = end - owner.Position;
            float range = spell.SpellData.CastRangeDisplayOverride;
            _destination = offset.LengthSquared() > range * range
                ? owner.Position + Vector2.Normalize(offset) * range : end;
            _destination = GetClosestTerrainExit(_destination, owner.PathfindingRadius);
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            int rank = spell.CastInfo.SpellLevel;
            AddParticle(owner, null, "JackintheboxPoof2", owner.Position, lifetime: 2, ignoreCasterVisibility: true);
            AddBuff("DeceiveCritBonus", spell.SpellData.GetEffectAmount(4, rank), 1, spell, owner, owner);
            AddBuff("Deceive", spell.SpellData.GetEffectAmount(3, rank), 1, spell, owner, owner);
            owner.TeleportTo(_destination.X, _destination.Y);
        }
    }
}
