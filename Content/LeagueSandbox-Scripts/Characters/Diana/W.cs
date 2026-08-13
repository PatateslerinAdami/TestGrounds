using AIScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class DianaOrbs : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = false
        };

        public int OrbsDetonated = 0;

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            PlayAnimation(owner, "Spell2", scaleTime: 1, startProgress: 0, speedRatio: 1, flags: AnimationFlags.Unknown7 | AnimationFlags.Unknown8);
            OrbsDetonated = 0;

            float radius = 150f;
            var orbSpell = owner.GetSpell("DianaOrbsMissile");

            for (int i = 0; i < 3; i++)
            {
                float angle = i * (120f * MathF.PI / 180f);
                Vector2 spawnPos = new Vector2(
                    owner.Position.X + radius * MathF.Cos(angle),
                    owner.Position.Y + radius * MathF.Sin(angle)
                );

                var parameters = new MissileParameters { Type = MissileType.Circle };
                orbSpell.CreateCustomMissile(spawnPos, spawnPos, parameters, target: owner);
            }
            CreateTimer(0.5f, () =>
            {
                StopAnimation(owner, fade: true, ignoreLock: true, stopAll: false, animation: "Spell2");
            });
        }
    }

    public class DianaOrbsMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters { Type = MissileType.Circle },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (missile == null)
            {
                return;
            }

            var owner = spell.CastInfo.Owner;
            float ap = owner.Stats.AbilityPower.Total * 0.2f;
            float damage = 22f + (12f * owner.GetSpell("DianaOrbs").CastInfo.SpellLevel) + ap;

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
            AddParticle(owner, target, "Diana_Base_W_Tar.troy",default);//?? does this even do anything lol 
            AddParticlePos(owner, "Diana_Base_W_Orb_End.troy", missile.Position, missile.Position);
            //Ahrifoxfire_obd-sound
            if (owner.GetSpell("DianaOrbs").Script is DianaOrbs parentScript)
            {
                parentScript.OrbsDetonated++;
                if (parentScript.OrbsDetonated >= 3)
                {
                    AddParticleTarget(owner, owner, "Diana_Base_W_Refreshed.troy", owner);
                }
            }

            missile.SetToRemove();
        }
    }
}