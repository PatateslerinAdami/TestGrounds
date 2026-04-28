using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class VelkozW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        // in VelkozWMissile.inibin changed [SpellData] MissileFollowsTerrainHeight to 0 because missile visual was sometimes going inside the terrain.
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

            var targetPos = startPos + (direction * 1200f);
            var dir3D = new Vector3(direction.X, 0, direction.Y);

            CreateCustomMissile(owner, "VelkozWMissile", startPos, targetPos,
                new MissileParameters { Type = MissileType.Circle }, customHeightOffset: -100f);
            PlayAnimation(owner, "Spell2", 1f);

            AddParticlePos(owner, "velkoz_base_w_telegraph_green.troy", startPos, targetPos, lifetime: 3f,
                direction: dir3D, teamOnly: owner.Team);
            AddParticlePos(owner, "velkoz_base_w_telegraph_red.troy", startPos, targetPos, lifetime: 3f,
                direction: dir3D, teamOnly: CustomConvert.GetEnemyTeam(owner.Team));
        }
    }

    public class VelkozWMissile : ISpellScript
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
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, false);
        }

        public void OnMissileHit(SpellMissile missile, AttackableUnit target)
        {
            var owner = missile.SpellOrigin.CastInfo.Owner;
            var ap = owner.Stats.AbilityPower.Total;
            var damage = 10f + (20f * missile.SpellOrigin.CastInfo.SpellLevel) + (ap * 0.15f);

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false,
                missile.SpellOrigin);
        }

        public void OnMissileEnd(SpellMissile missile)
        {
            var owner = missile.SpellOrigin.CastInfo.Owner;
            var startPos = new Vector2(missile.CastInfo.SpellCastLaunchPosition.X,
                missile.CastInfo.SpellCastLaunchPosition.Z);
            var endPos = missile.Position;
            var spell = missile.SpellOrigin;

            owner.RegisterTimer(new GameScriptTimer(0.25f, () => { DetonateRift(owner, spell, startPos, endPos); }));
        }

        private void DetonateRift(ObjAIBase owner, Spell spell, Vector2 startPos, Vector2 endPos)
        {
            var centerPos = (startPos + endPos) / 2f;
            var direction = Vector2.Normalize(endPos - startPos);
            var dir3D = new Vector3(direction.X, 0, direction.Y);

            AddParticlePos(owner, "velkoz_base_w_explode.troy", centerPos, endPos, lifetime: 1.0f, direction: dir3D);

            var ap = owner.Stats.AbilityPower.Total;
            var damage = 25f + (20f * spell.CastInfo.SpellLevel) + (ap * 0.25f);

            var units = GetUnitsInRange(owner, centerPos, 1200f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral);
            float halfWidth = 85f;

            foreach (var unit in units)
            {
                if (IsPointNearLineSegment(unit.Position, startPos, endPos, halfWidth + unit.CollisionRadius))
                {
                    unit.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                        false, spell);
                }
            }
        }

        private bool IsPointNearLineSegment(Vector2 point, Vector2 a, Vector2 b, float maxDistance)
        {
            float l2 = Vector2.DistanceSquared(a, b);
            if (l2 == 0) return Vector2.Distance(point, a) <= maxDistance;

            float t = System.Math.Max(0, System.Math.Min(1, Vector2.Dot(point - a, b - a) / l2));
            Vector2 projection = a + t * (b - a);

            return Vector2.Distance(point, projection) <= maxDistance;
        }
    }
}