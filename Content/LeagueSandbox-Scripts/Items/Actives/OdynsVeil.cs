using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class OdynsVeil : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false,
            CastingBreaksStealth = true,
            AutoFaceDirection = false
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ItemEffectHelpers.RestoreSharedActiveCooldown(owner, spell, 3180);
            ApiEventManager.OnCanCast.AddListener(this, owner, (unit, cast) =>
            {
                if (cast != spell)
                {
                    return true;
                }

                return ItemEffectHelpers.CanUseSharedActive(owner, spell, 3180);
            });
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!ItemEffectHelpers.CanUseSharedActive(owner, spell, 3180))
            {
                return;
            }

            var veilBuff = owner.GetBuffWithName("OdynsVeil")?.BuffScript as Buffs.OdynsVeil;
            if (veilBuff == null)
            {
                return;
            }

            var damage = veilBuff.Discharge();
            ItemEffectHelpers.ShareActiveCooldown(owner, spell, 3180, 90.0f);
            AddParticleTarget(owner, owner, "OdynsVeil_cas.troy", owner, 1.0f, ignoreCasterVisibility: true);
            var flags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions;
            foreach (var unit in GetUnitsInRange(owner, owner.Position, 500.0f, true, flags))
            {
                if (unit is Minion { IsWard: true })
                {
                    continue;
                }

                unit.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
