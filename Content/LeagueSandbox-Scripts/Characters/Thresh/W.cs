using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class ThreshW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true,
        };
        ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }
        public void OnSpellPostCast(Spell spell)
        {
            var targetPosEnd = spell.CastInfo.TargetPositionEnd;
            var targetPosEnd2D = new Vector2(targetPosEnd.X, targetPosEnd.Z);
            if (!IsWalkable(targetPosEnd2D.X, targetPosEnd2D.Y))
            {
                targetPosEnd2D = GetClosestTerrainExit(targetPosEnd2D);
            }
            SpellCast(_owner, 3, SpellSlotType.ExtraSlots, targetPosEnd2D, targetPosEnd2D, false, Vector2.Zero);


        }
    }
    public class ThreshWLanternOut : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true,
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Arc,
            }
        };
        ObjAIBase _owner;
        public Particle p;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile, false);
            _owner = owner;
        }
        public void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
        }

        public void OnMissileEnd(SpellMissile missile)
        {
            var lantern = AddMinion(_owner, "ThreshLantern", "ThreshLantern", missile.Position, _owner.Team, skinId: _owner.SkinID, ignoreCollision: true, targetable: false, isVisible: true, useSpells: true);
            AddParticle(_owner, lantern, "thresh_lantern.troy", default, teamOnly: _owner.Team);
            AddParticle(_owner, lantern, "Thresh_Lantern_Red.troy", default, teamOnly: CustomConvert.GetEnemyTeam(_owner.Team));
            AddParticle(_owner, lantern, "Thresh_LanternTimer.troy", default, teamOnly: _owner.Team);
            //AddParticle(_owner, lantern, "global_indicator_line_beam.troy", default, teamOnly: _owner.Team);
            p = AddParticleTarget(_owner, _owner, "thresh_w_lightshaft.troy", lantern, 6f, targetBone: "Lantern_Mid", bone: "C_Buffbone_Glb_Center_Loc");
            _owner.RegisterTimer(new GameScriptTimer(6f, () =>
            {
                if(lantern != null && !lantern.IsDead)
                    lantern.Die(CreateDeathData(false, 0, lantern, lantern, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
            }));

        }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            ScriptMetadata.MissileParameters.OverrideEndPosition = end;
        }
    }

}