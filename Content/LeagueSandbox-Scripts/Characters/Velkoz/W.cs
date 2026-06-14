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
using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class VelkozW : ISpellScript
    {

        private ObjAIBase _velkoz;
        private Vector2 _targetPos;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            AmmoPerCharge = 2,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _velkoz = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var startPos = owner.Position;
            var endPos = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);

            var direction = Vector2.Normalize(endPos - startPos);
            if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
            {
                direction = new Vector2(1, 0);
            }

            var targetPos = startPos + (direction * 1200f);
            _targetPos = targetPos;

            var dir3D = new Vector3(direction.X, 0, direction.Y);

            PlayAnimation(owner, "Spell2", 1f);

            // The W telegraph (the ground rift indicator) anchors to a TestCubeRender unit (Riot's
            // actual W anchor: replay c0896952 character="TestCubeRender" skin="Poot"). We spawn it
            // ignoreCollision:true → SpawnMinionS2C.IgnoreCollision tells the client not to collide
            // (the lightweight obj_AI_Marker we tried instead empirically blocked pathing at the
            // caster's position — confirmed in-game). targetable:false → not clickable. The
            // telegraph binds to it with TargetPosition = the off-map sentinel (-3679,-3706) so the
            // .troy draws its own FIXED-length rift from the anchor's orientation.
            var riftAnchor = AddMinion(owner, "TestCubeRender", "VelkozWRift", _velkoz.Position,
                owner.Team, ignoreCollision: true, targetable: false, isVisible: true, aiPaused: true,
                useSpells: false);
            HideHealthBar(riftAnchor);
            FaceDirection(targetPos, riftAnchor, isInstant: true);
            var offMapTarget = new Vector2(-367f, -421f); // int16-encodes to (-3679,-3706)
            AddParticle(owner, riftAnchor, "velkoz_base_w_telegraph_green.troy", offMapTarget,
                lifetime: 1.5f, enemyParticle: "velkoz_base_w_telegraph_red.troy", direction: dir3D,
                flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);
            // Despawn via Die() (NOT SetToRemove): GameObject.OnRemoved only drops server-side
            // collision/vision and never tells the client, so SetToRemove'd minions linger forever
            // (and pile up minimap dots). Die() broadcasts NotifyDeath so the client removes the
            // unit + its minimap dot. Internal/true damage, no killer credit. (Pattern: Vel'Koz E.)
            CreateTimer(2.5f, () => riftAnchor.Die(CreateDeathData(false, 0, riftAnchor, riftAnchor,
                DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0)));
        }
        
        public void OnSpellPostCast(Spell spell)
        {
            // Vel'Koz W has a ~0.251s windup before the lob launches (VelkozWMissile.OverrideCastTime
            // = 0.251; replay c0896952: telegraph -> missile launch = ~222ms). Vel'Koz can MOVE
            // during it (CantCancelWhileWindingUp=1 → not rooted, cast not cancelled) and the missile
            // still fires from the CAST-START position, not from where he moved to. So: capture the
            // launch position now (cast start), delay the missile by the windup, then fire it from
            // that captured spot via the overrideCastPos arg (_targetPos already encodes the original
            // direction/range from OnSpellPreCast).
            var launchPos = _velkoz.Position;
            CreateTimer(0.251f, () =>
                SpellCast(_velkoz, 1, SpellSlotType.ExtraSlots, _targetPos, _targetPos, true, launchPos));
        }
    }

    public class VelkozWMissile : ISpellScript
    {
        private ObjAIBase _velkoz;
        private Spell _spell;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Arc,
                // Vel'Koz W is a rift that opens THROUGH THE GROUND, not a lobbed projectile.
                // Replay c0896952 (W missile): Velocity.Y=0 and StartPoint/EndPoint Y = terrain
                // (53.4 / 51.7), i.e. it travels at GROUND level — implied height augment = 0.
                // VelkozWMissile.json carries no override so it inherited the default
                // MissileTargetHeightAugment=100, making our rift fly +100u up with a -141u/s
                // descent ramp — that's why the ground trail looked wrong / cut off. Force 0
                // (same fix as Aatrox E side missiles). JSON is the read-only client export, so
                // override here server-side.
                OverrideHeightAugment = 0f,
            },
            TriggersSpellCasts = false,
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _velkoz = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _spell = spell;
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile, false);
        }

        private void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            ApiEventManager.OnSpellMissileHit.AddListener(this, missile, OnMissileHit, false);
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, false);
        }

        private void OnMissileHit(SpellMissile missile, AttackableUnit target)
        {
            var ap = _velkoz.Stats.AbilityPower.Total * _velkoz.Spells[1].SpellData.Coefficient2;
            var damage = _velkoz.Spells[1].SpellData.EffectLevelAmount[2][_velkoz.Spells[1].CastInfo.SpellLevel]+ap;

            target.TakeDamage(_velkoz, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false,
                missile.SpellOrigin);

            // Hit FX attached to the struck unit (replay c0896952: velkoz_base_w_turret_tar.troy
            // has BindNetID = the hit unit's NetID, TargetNetID = 0 — i.e. bound to the target,
            // NOT a ground trail). One per unit the rift passes through on the way out.
            AddParticle(_velkoz, target, "velkoz_base_w_turret_tar.troy", target.Position, lifetime: 1.0f);
        }

        private void OnMissileEnd(SpellMissile missile)
        {
            var startPos = new Vector2(missile.CastInfo.SpellCastLaunchPosition.X,
                missile.CastInfo.SpellCastLaunchPosition.Z);
            var endPos = missile.Position;

            ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnLaunchMissile);
            ApiEventManager.OnSpellMissileHit.RemoveListener(this, missile, OnMissileHit);
            ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnMissileEnd);

            // The rift detonates 0.25s after the trail finishes forming (per the ability
            // description). Game-scoped timer so it still detonates if Vel'Koz dies during the
            // delay. start/end captured per-cast in the closure — this slot-singleton script
            // can't hold per-cast state otherwise.
            CreateTimer(0.25f, () => DetonateRift(startPos, endPos));
        }

        private void DetonateRift(Vector2 startPos, Vector2 endPos)
        {
            var direction = Vector2.Normalize(endPos - startPos);
            if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
            {
                direction = new Vector2(1, 0);
            }
            var dir3D = new Vector3(direction.X, 0, direction.Y);
            // The missile/trail flies the full CastRange (1200), but the DETONATION (damage +
            // explode FX) only covers the displayed range ~1050 (CastRangeDisplayOverride; replay
            // c0896952: explode span ~1016-1050 while the missile path is 1200 — the far ~150u of
            // trail never detonates). Cap the rift extent here so damage/FX match the wiki range.
            const float riftDamageRange = 1050f;
            var distance = MathF.Min(Vector2.Distance(startPos, endPos), riftDamageRange);
            var riftEnd = startPos + direction * distance;

            // The explode FX is NOT one stretched particle — Riot tiles fixed-size instances
            // along the rift, position-bound with no meaningful TargetPosition. Replay c0896952
            // shows EXACTLY 7 instances per cast in 73/73 casts (span ~1018u => ~170u spacing,
            // all sharing the cast-direction orientation). Our previous round(distance/169) gave a
            // VARIABLE count; fix to a constant 7 evenly spaced over the capped rift (step =
            // distance/6). Same tick → the FX batcher bundles them into one FX_Create_Group,
            // matching Riot's single packet with 7 FXCreateData entries.
            const int explodeCount = 7;
            // explodeCount is fixed at 7, so the >1 guard is always true; divide directly.
            float explodeStep = distance / (explodeCount - 1);
            for (int i = 0; i < explodeCount; i++)
            {
                var p = startPos + direction * (i * explodeStep);
                AddParticlePos(_velkoz, "velkoz_base_w_explode.troy", p, p, lifetime: 1.0f,
                    direction: dir3D, flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);
            }

            var mainSpell = _velkoz.Spells[1];
            // Rectangle covering the rift: x ±0.5×175 = ±87.5 (matches the missile's LineWidth
            // half-width / the OnMissileHit collision), y 0..distance (capped damage length).
            var unitsInPolygon = GetUnitsInPolygon(_velkoz, startPos, riftEnd - startPos, 175f, distance,
                [new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-0.5f, 1f)],
                true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectHeroes);
            foreach (var unit in unitsInPolygon)
            {
                var ap = _velkoz.Stats.AbilityPower.Total * mainSpell.SpellData.Coefficient2;
                var damage = mainSpell.SpellData.EffectLevelAmount[2][mainSpell.CastInfo.SpellLevel] + ap;
                
                AddParticle(_velkoz, unit, "velkoz_base_w_turret_tar.troy", unit.Position, lifetime: 1.0f);
                unit.TakeDamage(_velkoz, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
                // Detonation debuff, replay-verified on the wire (VelkozWDamage, 1.0s, on every
                // hit enemy). No buff script yet → BuffScriptEmpty (wire-only icon, harmless).
                AddBuff("VelkozWDamage", 1.0f, 1, mainSpell, unit, _velkoz);
            }
        }
    }
}