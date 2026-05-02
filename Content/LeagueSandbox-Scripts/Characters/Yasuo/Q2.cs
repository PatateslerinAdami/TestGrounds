using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using System.Numerics;

namespace Spells
{
    public class YasuoQ2W : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() { TriggersSpellCasts = true };
        public ObjAIBase _owner;
        Vector2 _start, _end;
        AttackableUnit _target;

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _owner = owner; _start = start; _end = end; _target = target;
        }

        public void OnSpellPostCast(Spell spell)
        {
            _owner.GetSpell("YasuoQ2").Cast(_start, _end, _target);

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

    public class YasuoQ2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() { TriggersSpellCasts = true, AutoFaceDirection = true };
        bool FirstTarget = true;
        bool _isCritRoll = false;

        public void OnActivate(ObjAIBase owner, Spell spell) { ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false); }
        
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            FaceDirection(end, owner, true);
            AddParticleTarget(owner, owner, "yasuo_base_q_windstrike_02.troy", owner, size: 0.9f);
            
            float bonusAS = owner.Stats.AttackSpeedMultiplier.Total - 1.0f;
            if (bonusAS < 0) bonusAS = 0;
            float castTimeReduction = bonusAS / 1.67f;
            float targetCastTime = 0.35f * (1f - castTimeReduction);
            if (targetCastTime < 0.18f) targetCastTime = 0.18f;
            
            spell.CastInfo.AttackSpeedModifier = 0.35f / targetCastTime;

            _isCritRoll = new System.Random().NextDouble() < owner.Stats.CriticalChance.Total;
            FirstTarget = true;
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            AddBuff("YasoAnimTest", 4f, 1, spell, owner, owner);
            AddParticleTarget(owner, owner, "Yasuo_Q2_Hand", owner);
            spell.CreateSpellSector(new SectorParameters
            {
                BindObject = owner, Length = 450f, Width = 100f,
                PolygonVertices = new Vector2[] { new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, 0) },
                SingleTick = true, Type = SectorType.Polygon
            });
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

                AddParticleTarget(owner, target, "Yasuo_Base_Q_hit_tar", target);
                AddParticleTarget(owner, owner, "yasuo_q2_ready_buff.troy", owner, size: 1f);
                if (owner.HasBuff("YasuoQ")) owner.RemoveBuffsWithName("YasuoQ");
                AddBuff("YasuoQ3W", 10f, 1, spell, owner, owner);

                FirstTarget = false;
                spell.CastInfo.IsAutoAttack = false; 
            }
            else
            {
                target.TakeDamage(owner, totalDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, _isCritRoll, spell);
            }
        }
    }
}