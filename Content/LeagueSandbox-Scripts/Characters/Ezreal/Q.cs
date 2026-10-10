using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
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
    public class EzrealMysticShot : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        private ObjAIBase _owner;
        private Spell _spell;
        private float _bonusAd = 0;
        private bool _isPulsefire;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
            ApiEventManager.OnUpdateStats.AddListener(this, owner, OnStatsUpdate, false);
            if (owner.SkinID == 5)
            {
                _isPulsefire = true;
            }
        }

        public void OnSpellCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            if (!_isPulsefire)
            {
                AddParticle(owner, owner, "ezreal_bow", Vector2.Zero, bone: "L_HAND");
            }
            else AddParticle(owner, owner, "ezreal_bow", Vector2.Zero, bone: "BUFFBONE_HAND_BOW");
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var targetPos = EzrealHelpers.Aim(spell);
            var distance = Vector2.Distance(owner.Position, targetPos);
            FaceDirection(targetPos, owner);

            if (distance > 1200.0f)
            {
                targetPos = GetPointFromUnit(owner, 1150.0f);
            }

            SpellCast(owner, owner.SkinID == 5 ? 3 : 0, SpellSlotType.ExtraSlots, targetPos, targetPos, false, Vector2.Zero, overrideForceLevel: spell.CastInfo.SpellLevel);

            //EzrealHelpers.CreateMysticShotFan(spell, -0.2f, 0.2f);
        }

        private void OnStatsUpdate(AttackableUnit _unit, float diff)
        {
            float bonusAd = _owner.Stats.AttackDamage.Total * _spell.SpellData.Coefficient;
            if (_bonusAd != bonusAd)
            {
                _bonusAd = bonusAd;
                SetSpellToolTipVar(_owner, 2, bonusAd, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
            }
        }
    }

    public class EzrealMysticShotMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle,
                ReplicateOnLaunch = true,
                SkipCastPacket = true
            },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunch);
        }

        private void OnLaunch(Spell spell, SpellMissile missile)
        {
            var owner = missile.CastInfo.Owner;
            var source = owner.GetSpell("EzrealMysticShot");
            var rank = missile.CastInfo.SpellLevel;
            var damage = source.SpellData.GetEffectAmount(1, rank)
                + owner.Stats.AttackDamage.Total * source.SpellData.Coefficient
                + owner.Stats.AbilityPower.Total * source.SpellData.Coefficient2;
            EzrealHelpers.OnHit(missile, target =>
            {
                target.TakeDamage(new DamageData
                {
                    Attacker = owner,
                    Target = target,
                    Damage = damage,
                    PostMitigationDamage = target.Stats.GetPostMitigationDamage(damage, DamageType.DAMAGE_TYPE_PHYSICAL, owner),
                    DamageType = DamageType.DAMAGE_TYPE_PHYSICAL,
                    DamageSource = DamageSource.DAMAGE_SOURCE_ATTACK,
                    IsAutoAttack = false,
                    AppliesOnHitEffects = true,
                    UsesLifeSteal = true
                }, false, source);
                for (byte slot = 0; slot < 4; slot++)
                {
                    owner.Spells[slot].LowerCooldown(1);
                }

                AddParticleTarget(owner, target, owner.SkinID == 5 ? "Ezreal_PulseFire_mysticshot_tar" : "Ezreal_mysticshot_tar", target, bone: owner.SkinID == 5 ? "": "C_BUFFBONE_GLB_CHEST_LOC");//bone: "C_BUFFBONE_GLB_CHEST_LOC"
                AddBuff("EzrealRisingSpellForce", 6, 1, source, owner, owner);
                missile.SetToRemove();
            });
        }
    }

    public class EzrealMysticShotPulseMissile : EzrealMysticShotMissile
    {
    }
}