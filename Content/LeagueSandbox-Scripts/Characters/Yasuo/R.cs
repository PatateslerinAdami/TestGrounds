using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class YasuoRKnockUpComboW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = true,
            CastTime = 1.0f 
        };

        public void OnActivate(ObjAIBase owner, Spell spell) { }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var targets = GetUnitsInRange(owner, owner.Position, 1200f, true, SpellDataFlags.AffectHeroes | SpellDataFlags.AffectEnemies)
                .Where(u => u.Team != owner.Team && u is ObjAIBase ai && (ai.HasBuff("YasuoQ3Mis") || ai.HasBuff("SKnock")))
                .Cast<ObjAIBase>().ToList();

            if (targets.Count == 0) return;

            owner.Stats.CurrentMana = owner.Stats.ManaPoints.Total;
            var mainTarget = targets[0];
            TeleportTo(owner, mainTarget.Position.X + 75f, mainTarget.Position.Y);
            FaceDirection(mainTarget.Position, owner, true);
            
            owner.PlayAnimation("Spell4", timeScale: 1.0f);

            foreach (var t in targets)
            {
                AddBuff("YasuoRStun", 1.25f, 1, spell, t, owner);
                AddBuff("YasuoRLeashParticle", 1.25f, 1, spell, t, owner);
            }

            AddBuff("YasuoRSound0", 1.0f, 1, spell, owner, owner);

            CreateTimer(0.2f, () => {
                AddBuff("YasuoRSound1", 1.0f, 1, spell, owner, owner);
                AddBuff("YasuoRVFXClone", 0.5f, 1, spell, owner, owner); 
                AddParticleTarget(owner, owner, "Yasuo_Base_R_slash", owner, 0.5f);
            });

            CreateTimer(0.5f, () => {
                AddBuff("YasuoRSound2", 1.0f, 1, spell, owner, owner);
                AddBuff("YasuoRVFXClone", 0.5f, 1, spell, owner, owner); 
                AddParticleTarget(owner, owner, "Yasuo_Base_R_slash", owner, 0.5f);
            });

            CreateTimer(0.8f, () => {
                AddBuff("YasuoRSound3", 1.0f, 1, spell, owner, owner);
                AddBuff("YasuoRVFXClone", 0.5f, 1, spell, owner, owner); 
                AddParticleTarget(owner, owner, "Yasuo_Base_R_slash", owner, 0.5f);
            });
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var targets = GetUnitsInRange(owner, owner.Position, 1200f, true, SpellDataFlags.AffectHeroes | SpellDataFlags.AffectEnemies)
                .Where(u => u.Team != owner.Team && u.HasBuff("YasuoRStun"))
                .Cast<ObjAIBase>().ToList();

            AddBuff("YasuoRSound4", 1.0f, 1, spell, owner, owner);
            AddBuff("YasuoRArmorPen", 15.0f, 1, spell, owner, owner);
                
            foreach (var target in targets)
            {
                target.RemoveBuffsWithName("YasuoRStun");

                float bonusAD = owner.Stats.AttackDamage.Total - owner.Stats.AttackDamage.BaseValue;
                float damage = 100f + (spell.CastInfo.SpellLevel * 100f) + (bonusAD * 1.5f);
                target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
                
                AddParticleTarget(owner, target, "Yasuo_Base_R_land_tar", target, 2.0f, 1, "root");
            }
        }
    }
}