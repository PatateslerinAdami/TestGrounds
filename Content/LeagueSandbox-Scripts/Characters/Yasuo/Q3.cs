using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class YasuoQ3W : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() { TriggersSpellCasts = true };
        public ObjAIBase _owner;
        Vector2 _start, _end;
        AttackableUnit _target;

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _owner = owner; _start = start; _end = end; _target = target;
            AddBuff("YasoAnimTest", 4f, 1, spell, owner, owner);
        }

        public void OnSpellPostCast(Spell spell)
        {
            _owner.GetSpell("YasuoQ3").Cast(_start, _end, _target);

            if (_owner.HasBuff("YasuoQ3W"))
            {
                _owner.RemoveBuffsWithName("YasuoQ3W");
            }

            int charLevel = _owner.Stats.Level;
            int trueQLevel = charLevel >= 9 ? 5 : charLevel >= 7 ? 4 : charLevel >= 5 ? 3 : charLevel >= 4 ? 2 : 1;

            float baseCooldown = 5.25f - (0.25f * trueQLevel);
            float bonusAS = _owner.Stats.AttackSpeedMultiplier.Total - 1.0f; 
            if (bonusAS < 0) bonusAS = 0;

            float cdReduction = bonusAS / 1.67f; 
            float finalCooldown = baseCooldown * (1f - cdReduction);
            
            if (finalCooldown < 1.33f) finalCooldown = 1.33f;
            _owner.Spells[0].SetCooldown(finalCooldown, true);
        }
    }

    public class YasuoQ3 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() { TriggersSpellCasts = true, AutoFaceDirection = true };
        
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            FaceDirection(end, owner, true);
            
            float bonusAS = owner.Stats.AttackSpeedMultiplier.Total - 1.0f;
            if (bonusAS < 0) bonusAS = 0;
            float castTimeReduction = bonusAS / 1.67f;
            float targetCastTime = 0.35f * (1f - castTimeReduction);
            if (targetCastTime < 0.18f) targetCastTime = 0.18f;
            
            spell.CastInfo.AttackSpeedModifier = 0.35f / targetCastTime;
        }
        
        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            AddParticleTarget(owner, owner, "Yasuo_Q3_Hand", owner);
            
            var targetPos = GetPointFromUnit(owner, 1100.0f);
            SpellCast(owner, 3, SpellSlotType.ExtraSlots, targetPos, targetPos, true, Vector2.Zero);
        }
    }

    public class YasuoQ3Mis : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters { Type = MissileType.Circle },
            IsDamagingSpell = true,
        };

        bool FirstTarget = true;
        bool _isCritRoll = false;

        public void OnActivate(ObjAIBase owner, Spell spell) 
        { 
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false); 
            spell.SetOverrideCastRange(1100.0f);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _isCritRoll = new System.Random().NextDouble() < owner.Stats.CriticalChance.Total;
            FirstTarget = true;
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;

            int charLevel = owner.Stats.Level;
            int trueQLevel = charLevel >= 9 ? 5 : charLevel >= 7 ? 4 : charLevel >= 5 ? 3 : charLevel >= 4 ? 2 : 1;

            float baseDamage = 20f * trueQLevel;
            float adScaling = owner.Stats.AttackDamage.Total;
            float totalDamage = baseDamage + adScaling;

            if (_isCritRoll)
            {
                float critMod = owner.Stats.CriticalDamage.Total - 0.25f;
                totalDamage = baseDamage + (adScaling * critMod);
            }

            if (FirstTarget)
            {
                if (spell.CastInfo.Targets == null) spell.CastInfo.Targets = new System.Collections.Generic.List<CastTarget>();
                spell.CastInfo.IsAutoAttack = true; 
                spell.CastInfo.SetTarget(target, 0);

                ApiEventManager.OnLaunchAttack.Publish(owner, spell);

                target.TakeDamage(owner, totalDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, _isCritRoll, spell);
                
                FirstTarget = false;
                spell.CastInfo.IsAutoAttack = false;
            }
            else
            {
                target.TakeDamage(owner, totalDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, _isCritRoll, spell);
            }

            AddBuff("YasuoQ3Mis", 1.2f, 1, spell, target, owner);
            AddParticleTarget(owner, target, "Yasuo_Base_Q_wind_hit_tar.troy", target);
            PlaySound("Play_sfx_Yasuo_YasuoQ_hit", owner);
            
        }
    }
}