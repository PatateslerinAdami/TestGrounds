using GameServerCore;
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
using System;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class XerathArcanopulseChargeUp : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            ChannelDuration = 3.0f,
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            AutoFaceDirection = false
        };
        ObjAIBase _owner;
        Particle p;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }

        public void OnSpellChannel(Spell spell)
        {
            AddBuff("XerathArcanopulseChargeUp", 3.0f, 1, spell, _owner, _owner);
            p = AddParticle(_owner, _owner, "Xerath_Base_Q_cas_charge.troy", _owner.Position, lifetime: 3.0f, bone: "BUFFBONE_GLB_CHANNEL_LOC");
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            LetGo();

            if (reason == ChannelingStopSource.PlayerCommand)
            {
                float maxChannelTime = ScriptMetadata.ChannelDuration;
                float timeChanneled = maxChannelTime - spell.CurrentChannelDuration;

                float minRange = 750f;
                float maxRange = 1400f;
                float growthDuration = 1.5f;

                float currentRange = minRange;
                if (timeChanneled > 0)
                {
                    float progress = Math.Min(1.0f, timeChanneled / growthDuration);
                    currentRange = minRange + ((maxRange - minRange) * progress);
                }
                Vector2 ownerPos = _owner.Position;
                Vector2 mousePos = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);

                Vector2 direction = Vector2.Normalize(mousePos - ownerPos);
                if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
                {
                    direction = new Vector2(1, 0);
                }
                Vector2 castPos = ownerPos + (direction * currentRange);

                _owner.GetSpell("XerathArcanopulse2").Cast(_owner.Position, castPos);
            }
        }

        public void OnSpellPostChannel(Spell spell)
        {
            LetGo();
        }
        private void LetGo()
        {
            if (_owner.HasBuff("XerathArcanopulseChargeUp")) _owner.RemoveBuffsWithName("XerathArcanopulseChargeUp");
            var timerAnm = new GameScriptTimer(0.4f, () =>
            {
                p.SetToRemove();
            });
            _owner.RegisterTimer(timerAnm);
        }
        public void OnUpdate(float diff)
        {
        }
    }

    public class XerathArcanopulse2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            owner.StopMovement();
            NotifyWaypointGroup(owner);
            FaceDirection(end, owner);
            PlayAnimation(owner, "Spell1Finish", 1.3f);

            AddParticlePos(owner, "xerath_base_q_aoe_reticle_green.troy", owner.Position, end, lifetime: 3.0f, teamOnly:owner.Team);
            AddParticlePos(owner, "xerath_base_q_aoe_reticle_red.troy", owner.Position, end, lifetime: 1.0f, teamOnly: CustomConvert.GetEnemyTeam(owner.Team), ignoreCasterVisibility: true);

            owner.SetStatus(StatusFlags.CanMove, false);
            owner.SetStatus(StatusFlags.CanCast, false);
            owner.SetStatus(StatusFlags.CanAttack, false);

            AddParticle(owner, owner, "xerath_base_q_cas.troy", owner.Position, lifetime: 3.0f);
            var timerAnm = new GameScriptTimer(0.5f, () =>
            {
                float height = owner.GetHeight() + 200f;
                //AddParticlePos(owner, "xerath_base_q_beam.troy", end, end, lifetime: 3.0f, unitOnly: owner, followGroundTilt: false, bone: "BUFFBONE_GLB_CHANNEL_LOC", overrideTargetHeight: height, ignoreCasterVisibility: true);
                AddParticle(owner, owner, "xerath_base_q_beam.troy", end, lifetime: 3.0f, overrideTargetHeight: height, bone: "BUFFBONE_GLB_CHANNEL_LOC");
                owner.SetStatus(StatusFlags.CanMove, true);
                owner.SetStatus(StatusFlags.CanCast, true);
                owner.SetStatus(StatusFlags.CanAttack, true);

                float dist = Vector2.Distance(start, end);
                spell.CreateSpellSector(new SectorParameters
                {
                    Length = dist,
                    Width = 145f,
                    SingleTick = true,
                    Type = SectorType.Polygon,
                    OverrideFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes,
                    PolygonVertices = new Vector2[]
                    {
                        new Vector2(-0.5f, 0f),
                        new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 1f),
                        new Vector2(-0.5f, 1f)
                    }
                });
            });
            owner.RegisterTimer(timerAnm);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            var ap = owner.Stats.AbilityPower.Total * 0.75f;
            var damage = ap + (40 * owner.Spells[0].CastInfo.SpellLevel) + 40;
            target.TakeDamage(owner, (float)damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
        }
    }
}