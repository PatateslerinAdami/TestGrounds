using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class VelkozQ : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var startPos = owner.Position;
            var endPos = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);

            var direction = Vector2.Normalize(endPos - startPos);
            if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
            {
                direction = new Vector2(1, 0);
            }

            var targetPos = startPos + (direction * 1050f);

            var missile = CreateCustomMissile(owner, "VelkozQMissile", startPos, targetPos, new MissileParameters { Type = MissileType.Circle });

            if (missile != null)
            {
                SetSpell(owner, "VelkozQSplitActivate", SpellSlotType.SpellSlots, 0);
                Vector3 direction3D = new Vector3(-direction.X, 0, -direction.Y);
                AddParticlePos(owner, "velkoz_base_q_endindicator.troy", targetPos, targetPos, lifetime: 1.5f, direction: direction3D, overrideTargetHeight: 100);
            }
        }
    }

    public class VelkozQMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle
            },
            IsDamagingSpell = true
        };

        public SpellMissile ActiveMissile;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile, false);
        }

        public void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            ActiveMissile = missile;
            ApiEventManager.OnSpellMissileHit.AddListener(this, missile, OnMissileHit, false);
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, false);
        }

        public void OnMissileHit(SpellMissile missile, AttackableUnit target)
        {
            var owner = missile.SpellOrigin.CastInfo.Owner;
            var ap = owner.Stats.AbilityPower.Total;
            var damage = 40f + (40f * missile.SpellOrigin.CastInfo.SpellLevel) + (ap * 0.6f);

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, missile.SpellOrigin);
            AddBuff("VelkozQSlow", 1.0f + (0.25f * missile.SpellOrigin.CastInfo.SpellLevel), 1, missile.SpellOrigin, target, owner);
            AddBuff("VelkozQSplitImmunity", 0.5f, 1, missile.SpellOrigin, target, owner);
            AddParticleTarget(owner, null, "velkoz_base_q_missile_tar.troy", target, lifetime: 1.0f);
            missile.SetToRemove();
        }

        public void OnMissileEnd(SpellMissile missile)
        {
            var owner = missile.SpellOrigin.CastInfo.Owner;

            SetSpell(owner, "VelkozQ", SpellSlotType.SpellSlots, 0);

            Vector2 currentDir = new Vector2(missile.Direction.X, missile.Direction.Z);
            if (currentDir == Vector2.Zero)
            {
                currentDir = new Vector2(1, 0);
            }
            currentDir = Vector2.Normalize(currentDir);

            Vector2 leftDir = new Vector2(-currentDir.Y, currentDir.X);
            Vector2 rightDir = new Vector2(currentDir.Y, -currentDir.X);

            float splitRange = 1100f;
            Vector2 leftEnd = missile.Position + (leftDir * splitRange);
            Vector2 rightEnd = missile.Position + (rightDir * splitRange);

            Vector3 forward3D = new Vector3(currentDir.X, 0, currentDir.Y);
            AddParticlePos(owner, "velkoz_base_q_splitimplosion.troy", missile.Position, missile.Position, lifetime: 1.0f, direction: forward3D, overrideTargetHeight:100);
            owner.RegisterTimer(new GameScriptTimer(0.25f, () =>
            {
                //AddParticlePos(owner, "velkoz_base_q_splitimplosion.troy", missile.Position, missile.Position, lifetime: 1.0f, direction: forward3D);
                AddParticlePos(owner, "velkoz_base_q_splitexplosion.troy", missile.Position, missile.Position, lifetime: 1.0f, direction: forward3D, overrideTargetHeight: 100);


                CreateCustomMissile(owner, "VelkozQMissileSplit", missile.Position, leftEnd, new MissileParameters { Type = MissileType.Circle });
                CreateCustomMissile(owner, "VelkozQMissileSplit", missile.Position, rightEnd, new MissileParameters { Type = MissileType.Circle });
            }));
        }
    }

    public class VelkozQSplitActivate : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = false
        };

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var missileSpell = owner.GetSpell("VelkozQMissile");

            if (missileSpell != null && missileSpell.Script is VelkozQMissile missileScript)
            {
                if (missileScript.ActiveMissile != null && !missileScript.ActiveMissile.IsToRemove())
                {
                    missileScript.ActiveMissile.SetToRemove();
                }
            }

            SetSpell(owner, "VelkozQ", SpellSlotType.SpellSlots, 0);
        }
    }

    public class VelkozQMissileSplit : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle
            },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile, false);
        }

        public void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            ApiEventManager.OnSpellMissileHit.AddListener(this, missile, OnMissileHit, false);
        }

        public void OnMissileHit(SpellMissile missile, AttackableUnit target)
        {
            if (HasBuff(target, "VelkozQSplitImmunity"))
            {
                return;
            }

            var owner = missile.SpellOrigin.CastInfo.Owner;
            var qSpell = owner.GetSpell("VelkozQ");
            int spellLevel = qSpell != null ? qSpell.CastInfo.SpellLevel : 1;

            var ap = owner.Stats.AbilityPower.Total;
            var damage = 40f + (40f * spellLevel) + (ap * 0.6f);

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, missile.SpellOrigin);
            //AddBuff("VelkozQSlow", 1.0f + (0.25f * spellLevel), 1, missile.SpellOrigin, target, owner);
            AddParticleTarget(owner, null, "velkoz_base_q_missile_tar.troy", target, lifetime: 1.0f);
            missile.SetToRemove();
        }
    }
}

namespace Buffs
{
    public class VelkozQSplitImmunity : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }
}