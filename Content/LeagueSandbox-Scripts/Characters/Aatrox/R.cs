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
    public class AatroxR : ISpellScript
    {
        private float _damage;
        private byte _rank;

        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _rank = spell.CastInfo.SpellLevel;
            _damage = 100 + 100 * _rank + owner.Stats.AbilityPower.Total;
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            AddParticleTarget(owner, owner, AatroxHelpers.Effect(owner, "R_Activate"), owner, 0.5f);
            var buff = AddBuff("AatroxR", 12, 1, spell, owner, owner);
            if (buff.BuffScript is Buffs.AatroxR r)
            {
                r.SetRank(_rank);
            }

            foreach (var enemy in GetUnitsInRange(owner, owner.Position, 550, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes))
            {
                if (enemy is not Champion && enemy is not Pet { IsClone: true })
                {
                    continue;
                }

                AddParticleTarget(owner, enemy, AatroxHelpers.Effect(owner, "R_active_hit_tar"), enemy, 0.5f);
                enemy.TakeDamage(owner, _damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
                SpellCast(owner, 5, SpellSlotType.ExtraSlots, true, owner, enemy.Position);
            }
        }
    }

    public class AatroxRHeal : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters()
            {
                Type= MissileType.Target,
                IsVisualOnly = true
            }
        };
    }
}