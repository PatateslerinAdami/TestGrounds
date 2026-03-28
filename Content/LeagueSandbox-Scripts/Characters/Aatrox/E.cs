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
using System.Collections.Generic;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class AatroxE : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        public void OnSpellCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var blood = owner.Stats.CurrentHealth * 0.05f;
            owner.Stats.CurrentMana += blood;
            owner.TakeDamage(owner, blood, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_PERIODIC, false);
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            AddParticleTarget(owner, owner, "Aatrox_Base_E_Glow.troy", owner, bone: "Spine3");

            var lPos = GetPointFromUnit(owner, 175f, -35f);
            var rPos = GetPointFromUnit(owner, 175f, 35f);
            var targetPos = GetPointFromUnit(owner, 1200.0f);

            var coneSpell = owner.GetSpell("AatroxEConeMissile");
            if (coneSpell != null && coneSpell.Script is AatroxEConeMissile coneScript)
            {
                coneScript.HitUnits.Clear();
            }

            var missileParams = new MissileParameters { Type = MissileType.Circle };

            CreateCustomMissile(owner, "AatroxEConeMissile", lPos, targetPos, missileParams, customHeightOffset: -100f);
            CreateCustomMissile(owner, "AatroxEConeMissile", rPos, targetPos, missileParams, customHeightOffset: -100f);
        }
    }
    public class AatroxEConeMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle
            },
            IsDamagingSpell = true,
            TriggersSpellCasts = true
        };

        public List<AttackableUnit> HitUnits = new List<AttackableUnit>();

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (HitUnits.Contains(target)) return;
            HitUnits.Add(target);

            var owner = spell.CastInfo.Owner;
            float ap = owner.Stats.AbilityPower.Total * 0.6f;
            float ad = owner.Stats.AttackDamage.Total * 0.6f;
            float t = 1.75f + (spell.CastInfo.SpellLevel - 1) * 0.25f;
            float damage = 75 + (spell.CastInfo.SpellLevel - 1) * 35 + ad + ap;

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
            AddBuff("AatroxESlow", t, 1, spell, target, owner);
            AddParticleTarget(owner, target, "Aatrox_Base_EMissile_Hit.troy", target);
        }
    }
}