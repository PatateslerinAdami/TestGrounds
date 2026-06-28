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
            // Model name must match the case-sensitive asset lookup ("TestCubeRender10Vision"),
            // otherwise AddMinion returns null, _toLookAt stays null, OnActivate NREs silently
            // via the try/catch in Spell.cs, and OnSpellPreCast then NREs on the null deref.
            _toLookAt = AddMinion(_owner, "TestCubeRender10Vision", "YasuoWLookAt", owner.Position, owner.Team, ignoreCollision: true, targetable: false, useSpells: false);
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
            // TODO(sub-missile-replay-audit): Yasuo W (and other script-spawned sub-missiles)
            // have been silently invisible since commit 67354ff1 (2026-05-10) added the
            // primary-missile shortcut in ConstructSpawnPacket. Postponed pending replay
            // verification of the correct wire pattern. See memory note
            // [[deferred-sub-missile-replication]] for context.
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

        // Server-side AreaTriggerWall id (Riot AreaTriggerWall / Windwall). -1 = none.
        int _wallId = -1;

        private void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            _wallWidth = 300f + (50f * (spell.CastInfo.SpellLevel - 1));

            // Create the Windwall as an AreaTriggerWall instead of hand-scanning GetMissiles() every tick:
            // the central missile path (SpellMissile.PublishOnSpellMissileUpdate -> AreaTriggerManager.
            // TryDestroyMissile) destroys crossing enemy missiles. The segment [p1,p2] spans the wall width;
            // thickness 60 (half=30) reproduces the old "CollisionRadius + 30" catch band exactly. We steer
            // the endpoints each tick (OnMissileUpdate) and delete the wall on missile end.
            var (p1, p2) = ComputeWallEndpoints(missile);
            _wallId = CreateAreaTriggerWall(p1, p2, 60f, destroysMissiles: true, _owner.Team);

            ApiEventManager.OnSpellMissileUpdate.AddListener(this, missile, OnMissileUpdate, false);
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd);
        }

        private (Vector2 p1, Vector2 p2) ComputeWallEndpoints(SpellMissile wallMissile)
        {
            Vector2 wallPos = wallMissile.Position;
            Vector2 wallDir = new Vector2(wallMissile.Direction.X, wallMissile.Direction.Z);
            if (wallDir.LengthSquared() == 0)
            {
                wallDir = new Vector2(_owner.Direction.X, _owner.Direction.Z);
            }
            wallDir = Vector2.Normalize(wallDir);
            Vector2 perp = new Vector2(-wallDir.Y, wallDir.X);
            return (wallPos + perp * (_wallWidth / 2f), wallPos - perp * (_wallWidth / 2f));
        }

        private void OnMissileUpdate(SpellMissile wallMissile, float diff)
        {
            var (p1, p2) = ComputeWallEndpoints(wallMissile);
            UpdateAreaTriggerWallEndpoints(_wallId, p1, p2);
        }

        private void OnMissileEnd(SpellMissile missile)
        {
            DeleteAreaTrigger(_wallId);
            _wallId = -1;
        }
    }
}