using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class VelkozQ : ISpellScript
    {

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = false,
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

            // Share this cast's per-cast variable bag with the missile sub-cast (Riot's
            // SpellCastInfo::LuaVars model), so per-cast state flows parent->missile without
            // a cross-script field reference.
            SpellCast(owner, 0, SpellSlotType.ExtraSlots, targetPos, targetPos, true, Vector2.Zero,
                inheritVariablesFrom: spell.CastInfo);

            SetSpell(owner, "VelkozQSplitActivate", SpellSlotType.SpellSlots, 0);
            Vector3 direction3D = new Vector3(-direction.X, 0, -direction.Y);
            // flags 0x30 (UpdateOrientation | SimulateWhileOffScreen) — replay-verified: Riot
            // always sets the GivenDirection bit so the client uses the OrientationVector. The
            // default (SimulateWhileOffScreen only) dropped it.
            // KeywordNetID=0 (replay-verified: generic ground indicator, no skin-color source,
            // m_KeywordObjectID=0) is now the wire default — no explicit override needed.
            var endIndicator = AddParticlePos(owner, "velkoz_base_q_endindicator.troy", targetPos, targetPos,
                lifetime: 1.5f, direction: direction3D, overrideTargetHeight: 100,
                flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);
            // Stash in the shared per-cast bag so VelkozQMissile.OnMissileEnd can kill it the
            // moment the bolt reaches the endpoint. Replay: Riot FX_Kills the indicator on
            // Q-end (~0.65s, variable — hit/max-range/recast), NOT a fixed timer; the 1.5s
            // lifetime is only a backstop.
            spell.CastInfo.InstanceVars.Set("QEndIndicator", endIndicator);
        }
    }

    public class VelkozQMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Arc
            },
            NotSingleTargetSpell = false,
            DoesntBreakShields = true,
            TriggersSpellCasts = false,
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile, false);
        }

        private void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            ApiEventManager.OnSpellMissileHit.AddListener(this, missile, OnMissileHit, false);
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, false);
        }

        private void OnMissileHit(SpellMissile missile, AttackableUnit target)
        {
            var owner = missile.SpellOrigin.CastInfo.Owner;
            var ap = owner.Stats.AbilityPower.Total;
            var damage = 40f + (40f * missile.SpellOrigin.CastInfo.SpellLevel) + (ap * 0.6f);

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false,
                missile.SpellOrigin);
            AddBuff("VelkozQSlow", 1.0f + (0.25f * missile.SpellOrigin.CastInfo.SpellLevel), 1, missile.SpellOrigin,
                target, owner);
            // Record the hit in the shared per-cast bag (Riot's LuaVars pattern) so the split
            // missiles — which inherit this same InstanceVars bag — won't hit this target again.
            VelkozQHitTracking.GetHitSet(missile.SpellOrigin.CastInfo).Add(target.NetId);
            AddParticleTarget(owner, null, "velkoz_base_q_missile_tar.troy", target, lifetime: 1.0f);
            missile.SetToRemove();
        }

        public void OnMissileEnd(SpellMissile missile)
        {
            var owner = missile.SpellOrigin.CastInfo.Owner;

            // Carry the original Q cast's level + per-cast variable bag into the split casts,
            // so the split missiles damage off their own CastInfo/SpellData (no cross-script
            // GetSpell("VelkozQ") grab in VelkozQMissileSplit).
            var parentCastInfo = missile.SpellOrigin.CastInfo;
            int qLevel = parentCastInfo.SpellLevel;

            // Kill the end indicator now that the bolt has reached the endpoint (matches Riot's
            // FX_Kill-on-Q-end). The particle was stashed in the shared per-cast bag by
            // VelkozQ.OnSpellPostCast.
            if (parentCastInfo.InstanceVars.TryGet<Particle>("QEndIndicator", out var endIndicator))
            {
                RemoveParticle(endIndicator);
            }

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
            AddParticlePos(owner, "velkoz_base_q_splitimplosion.troy", missile.Position, missile.Position,
                lifetime: 1.0f, direction: forward3D, overrideTargetHeight: 100);
            owner.RegisterTimer(new GameScriptTimer(0.25f, () =>
            {
                //AddParticlePos(owner, "velkoz_base_q_splitimplosion.troy", missile.Position, missile.Position, lifetime: 1.0f, direction: forward3D);
                AddParticlePos(owner, "velkoz_base_q_splitexplosion.troy", missile.Position, missile.Position,
                    lifetime: 1.0f, direction: forward3D, overrideTargetHeight: 100);


                // ExtraSlot 5 = VelkozQMissileSplit (ExtraSpell6). fireWithoutCasting skips the
                // windup; overrideCastPos launches the Arc missile from the implosion point
                // (like Talon W return blades); overrideForceLevel + inheritVariablesFrom give
                // the split the original Q cast's level + shared variable bag.
                SpellCast(owner, 5, SpellSlotType.ExtraSlots, leftEnd, leftEnd, true, missile.Position,
                    overrideForceLevel: qLevel, inheritVariablesFrom: parentCastInfo);
                SpellCast(owner, 5, SpellSlotType.ExtraSlots, rightEnd, rightEnd, true, missile.Position,
                    overrideForceLevel: qLevel, inheritVariablesFrom: parentCastInfo);
            }));
        }
    }

    public class VelkozQSplitActivate : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = false,
            DoesntBreakShields = true,
            TriggersSpellCasts = true,
            CastingBreaksStealth = false,
            IsDamagingSpell = false
        };

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;

            // End the active Q missile (looked up via the engine missile registry, not a
            // cross-script field) — that fires VelkozQMissile.OnMissileEnd, which spawns the
            // split missiles and swaps the slot back to VelkozQ (same path as a max-range
            // split). If the missile is already gone, the SetSpell below restores the slot.
            var activeMissile = GetMissilesByOwnerAndSpell(owner, "VelkozQMissile").Find(m => !m.IsToRemove());
            activeMissile?.SetToRemove();

            SetSpell(owner, "VelkozQ", SpellSlotType.SpellSlots, 0);
        }
    }

    public class VelkozQMissileSplit : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Arc
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
            // Skip targets already hit by the main bolt or the other split bolt (shared
            // per-cast hit-set — Riot tracks "hit once per Q cast" in the cast's LuaVars, not
            // a buff). HashSet.Add returns false when the unit is already present.
            if (!VelkozQHitTracking.GetHitSet(missile.SpellOrigin.CastInfo).Add(target.NetId))
            {
                return;
            }

            var owner = missile.SpellOrigin.CastInfo.Owner;
            // SpellLevel was forced to the original Q cast's level when the split was cast
            // (see VelkozQMissile.OnMissileEnd), so the damage reads off this missile's own
            // SpellData — no cross-script GetSpell("VelkozQ") lookup.
            int spellLevel = missile.SpellOrigin.CastInfo.SpellLevel;

            var ap = owner.Stats.AbilityPower.Total;
            var damage = missile.SpellOrigin.SpellData.EffectLevelAmount[1][spellLevel] + (ap * 0.6f);

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false,
                missile.SpellOrigin);
            //AddBuff("VelkozQSlow", 1.0f + (0.25f * spellLevel), 1, missile.SpellOrigin, target, owner);
            AddParticleTarget(owner, null, "velkoz_base_q_missile_tar.troy", target, lifetime: 1.0f);
            missile.SetToRemove();
        }
    }

    /// <summary>
    /// "Hit once per Q cast" tracking. The set lives in the shared per-cast InstanceVars bag
    /// (CastInfo.InstanceVars = Riot's LuaVars), which the main VelkozQMissile and both split
    /// VelkozQMissileSplit missiles share via SpellCast(inheritVariablesFrom:). Replaces the
    /// old VelkozQSplitImmunity buff hack — Riot has no such buff (its de-dup is engine/cast
    /// state, not a debuff on the target).
    /// </summary>
    internal static class VelkozQHitTracking
    {
        private const string HitSetKey = "QHitUnits";

        public static HashSet<uint> GetHitSet(CastInfo castInfo)
        {
            if (!castInfo.InstanceVars.TryGet<HashSet<uint>>(HitSetKey, out var set))
            {
                set = new HashSet<uint>();
                castInfo.InstanceVars.Set(HitSetKey, set);
            }
            return set;
        }
    }
}