using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class YasuoWMovingWall : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            AutoFaceDirection = false
        };

        Vector2 dir;
        ObjAIBase _owner;
        Minion _toLookAt;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell, true);
            _owner = owner;
            _toLookAt = AddMinion(_owner, "testcuberender10vision", "testcuberender10vision", owner.Position, owner.Team, ignoreCollision: true, targetable: false, useSpells: false);
        }

        private void OnLevelUpSpell(Spell spell)
        {
            SetSpell(spell.CastInfo.Owner, "YasuoWMovingWallMisVis", SpellSlotType.ExtraSlots, 4);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            dir = (end - owner.Position).Normalized();

            var endPos = _owner.Position + dir * 400f;
            _toLookAt.SetPosition(endPos);
            UnitSetLookAt(_owner, _toLookAt, AttackType.ATTACK_TYPE_MELEE);
        }

        public void OnSpellPostCast(Spell spell)
        {
            var m = CreateCustomMissile(_owner, "YasuoWMovingWallMisVis", _owner.Position, _owner.Position + dir * 800f, new MissileParameters { Type = MissileType.Arc }, customHeightOffset: -50f);
            AddParticleTarget(_owner, m, "Yasuo_Base_W_windwall4", m, lifetime: 3.75f);

            _owner.RegisterTimer(new GameScriptTimer(3.75f, () =>
            {
                m.SetToRemove();
            }));
        }
    }

    public class YasuoWMovingWallMisVis : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters { Type = MissileType.Arc },
            IsDamagingSpell = false
        };

        ObjAIBase _owner;
        float _wallWidth;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile, false);
        }

        private void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            _wallWidth = 300f + (50f * (spell.CastInfo.SpellLevel - 1));
            ApiEventManager.OnSpellMissileUpdate.AddListener(this, missile, OnMissileUpdate, false);
        }

        private void OnMissileUpdate(SpellMissile wallMissile, float diff)
        {
            Vector2 wallPos = wallMissile.Position;
            Vector2 wallDir = new Vector2(wallMissile.Direction.X, wallMissile.Direction.Z);

            if (wallDir.LengthSquared() == 0)
            {
                wallDir = new Vector2(_owner.Direction.X, _owner.Direction.Z);
            }
            wallDir = Vector2.Normalize(wallDir);

            Vector2 perp = new Vector2(-wallDir.Y, wallDir.X);

            Vector2 p1 = wallPos + perp * (_wallWidth / 2f);
            Vector2 p2 = wallPos - perp * (_wallWidth / 2f);

            var allMissiles = GetMissiles();

            foreach (var enemyMissile in allMissiles)
            {
                if (enemyMissile == null || enemyMissile.IsToRemove() || enemyMissile.Team == _owner.Team || enemyMissile == wallMissile)
                    continue;

                float distanceToWall = GetDistanceToSegment(enemyMissile.Position, p1, p2);
                float hitThreshold = enemyMissile.CollisionRadius + 30.0f;

                if (distanceToWall <= hitThreshold)
                {
                    enemyMissile.SetToRemove();

                }
            }
        }
        private float GetDistanceToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            Vector2 v = segmentEnd - segmentStart;
            Vector2 w = point - segmentStart;

            float c1 = Vector2.Dot(w, v);
            if (c1 <= 0)
                return Vector2.Distance(point, segmentStart);

            float c2 = Vector2.Dot(v, v);
            if (c2 <= c1)
                return Vector2.Distance(point, segmentEnd);

            float b = c1 / c2;
            Vector2 closestPoint = segmentStart + v * b;
            return Vector2.Distance(point, closestPoint);
        }
    }
}