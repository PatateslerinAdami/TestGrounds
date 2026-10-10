using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static GameServerCore.Content.HashFunctions;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class EzrealEssenceFlux : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        public void OnSpellCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            AddParticleTarget(owner, owner, "ezreal_bow_yellow", owner, bone: "L_HAND");
        }
        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var targetPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            var distance = Vector2.Distance(owner.Position, targetPos);
            FaceDirection(targetPos, owner);

            if (distance > 1200.0)
            {
                targetPos = GetPointFromUnit(owner, 1150.0f);
            }
            SpellCast(owner, 2, SpellSlotType.ExtraSlots, targetPos, targetPos, false, Vector2.Zero,
                overrideForceLevel: spell.CastInfo.SpellLevel);
        }
    }

    public class EzrealEssenceFluxMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle,
                ReplicateOnLaunch = true
            },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunch);
        }

        private void OnLaunch(Spell spell, SpellMissile missile)
        {
            var owner = spell.CastInfo.Owner;
            var source = owner.GetSpell("EzrealEssenceFlux");
            var rank = missile.CastInfo.SpellLevel;
            var damage = source.SpellData.GetEffectAmount(1, rank)
                + owner.Stats.AbilityPower.Total * source.SpellData.Coefficient;
            EzrealHelpers.OnHit(missile, target =>
            {
                if (target.Team != owner.Team)
                {
                    target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, source);
                    AddParticleTarget(owner, target, owner.SkinID == 5 ? "Ezreal_Pulsefire_essenceflux_tar" : "Ezreal_essenceflux_tar", target, bone: owner.SkinID == 5 ? "" : "C_BUFFBONE_GLB_CHEST_LOC");
                }
                else
                {
                    var variables = new BuffVariables();
                    variables.Set("attackSpeed", source.SpellData.GetEffectAmount(2, rank) / 100);
                    AddBuff("EzrealEssenceFlux", 5, 1, source, target, owner, buffVariables: variables);
                }

                AddBuff("EzrealRisingSpellForce", 6, 1, source, owner, owner);
            });
        }
    }
}