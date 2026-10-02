using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemBlackfireTorch : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false,
            CastingBreaksStealth = true,
            DoesntBreakShields = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ItemEffectHelpers.RestoreSharedActiveCooldown(owner, spell, 3188);
            ApiEventManager.OnCanCast.AddListener(this, owner, (unit, cast) =>
            {
                if (cast != spell)
                {
                    return true;
                }

                return ItemEffectHelpers.CanUseSharedActive(owner, spell, 3188);
            });
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!ItemEffectHelpers.CanUseSharedActive(owner, spell, 3188))
            {
                return;
            }

            if (target is not Champion || !IsValidTarget(owner, target, spell.SpellData.Flags))
            {
                return;
            }

            ItemEffectHelpers.ShareActiveCooldown(owner, spell, 3188, 90.0f);
            SpellCastItem(owner, "ItemBlackfireTorchSpell", true, target, Vector2.Zero);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}

namespace Spells
{
    public class ItemBlackfireTorchSpell : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            },
            DoesntBreakShields = true,
            IsDamagingSpell = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnHit, true);
        }

        private void OnHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (target is Champion && !target.IsDead && target.Team != spell.CastInfo.Owner.Team)
            {
                AddParticleTarget(
                    spell.CastInfo.Owner,
                    target,
                    "ItemBlackfireTorch_tar.troy",
                    target,
                    1.0f,
                    ignoreCasterVisibility: true
                );
                AddBuff("ItemBlackfireTorchSpell", 4.0f, 1, spell, target, spell.CastInfo.Owner);
            }

            if (missile != null)
            {
                missile.SetToRemove();
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
