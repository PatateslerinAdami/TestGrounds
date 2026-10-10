using LeagueSandbox.GameServer.GameObjects;
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
    public class JackInTheBox : ISpellScript
    {
        private Vector2 _position;
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            TriggersSpellCasts = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!float.IsFinite(end.X) || !float.IsFinite(end.Y))
            {
                spell.CancelCast();
                return;
            }
            _position = GetClosestTerrainExit(end);
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            int rank = spell.CastInfo.SpellLevel;
            var box = AddMinion(owner, "ShacoBox", "ShacoBox", _position, owner.Team,
                skinId: owner.SkinID, ignoreCollision: true, targetingFlags: SpellDataFlags.TargetableToAll);
            box.SetStatus(StatusFlags.CanMove, false);
            box.Stats.ManaRegeneration.BaseValue = 0;
            box.Stats.GoldGivenOnDeath.BaseValue = 10;
            box.Stats.ExpGivenOnDeath.BaseValue = 0;
            FaceDirection(_position + new Vector2(owner.Direction.X, owner.Direction.Z), box);
            AddParticle(owner, null, "JackintheboxPoof", _position, lifetime: 2, ignoreCasterVisibility: true);

            var variables = new BuffVariables();
            box.Stats.AttackDamage.BaseValue = spell.SpellData.GetEffectAmount(1, rank)
                + owner.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
            variables.Set("FearDuration", spell.SpellData.GetEffectAmount(3, rank));
            variables.Set("HiddenDuration", spell.SpellData.GetEffectAmount(4, rank));
            AddBuff("JackInTheBoxInternal", 2 + spell.SpellData.GetEffectAmount(4, rank) + 5, 1,
                spell, box, owner, buffVariables: variables);
        }
    }
}
