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
    public class ZedShuriken : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        private ObjAIBase _owner;

        private Minion _wShadow;
        private Minion wShadow
        {
            get
            {
                if (_wShadow == null)
                {
                    var sp = _owner.GetSpell("ZedShadowDashMissile");
                    if (sp?.Script is ZedShadowDashMissile dashScript)
                    {
                        _wShadow = dashScript.shadow;
                    }
                }
                return _wShadow;
            }
        }

        private ZedShadowDashMissile GetInFlightMissile(ObjAIBase owner)
        {
            var sp = owner.GetSpell("ZedShadowDashMissile");
            if (sp?.Script is ZedShadowDashMissile dashScript && dashScript.MissileInFlight)
            {
                return dashScript;
            }
            return null;
        }

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            owner.StopMovement(networked: false);

            var dashScript = GetInFlightMissile(owner);

            if (dashScript != null)
            {
                var capturedEnd = end;
                dashScript.PendingShadowCasts.Add(() =>
                {
                    FaceDirection(capturedEnd, dashScript.shadow);
                    dashScript.shadow.PlayAnimation("Spell1", timeScale: 1.5f);
                });
            }
            else if (wShadow != null)
            {
                FaceDirection(end, wShadow);
                wShadow.PlayAnimation("Spell1", timeScale: 1.5f);
            }
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;

            var targetPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            var maxRange = spell.GetCurrentCastRange();
            var zedDir = Vector2.Normalize(targetPos - owner.Position);
            var zedEndPos = owner.Position + (zedDir * maxRange);

            SpellCast(owner, 1, SpellSlotType.ExtraSlots, zedEndPos, zedEndPos, true, Vector2.Zero);

            var dashScript = GetInFlightMissile(owner);

            if (dashScript != null)
            {
                var capturedTargetPos = targetPos;
                var capturedMaxRange = maxRange;
                dashScript.PendingShadowCasts.Add(() =>
                {
                    var sh = dashScript.shadow;
                    var shadowDir = Vector2.Normalize(capturedTargetPos - sh.Position);
                    var shadowEndPos = sh.Position + (shadowDir * capturedMaxRange);
                    CreateCustomMissile(owner, "ZedShurikenMisOne", sh.Position, shadowEndPos,
                        new MissileParameters { Type = MissileType.Circle });
                });
            }
            else if (owner.HasBuff("ZedWHandler2"))
            {
                var sh = wShadow;
                if (sh != null)
                {
                    var shadowDir = Vector2.Normalize(targetPos - sh.Position);
                    var shadowEndPos = sh.Position + (shadowDir * maxRange);
                    CreateCustomMissile(owner, "ZedShurikenMisOne", sh.Position, shadowEndPos,
                        new MissileParameters { Type = MissileType.Circle });
                }
            }
        }
    }

    public class ZedShurikenMisOne : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters { Type = MissileType.Circle },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            var ad = owner.Stats.AttackDamage.Total * spell.SpellData.Coefficient;
            var ap = owner.Stats.AbilityPower.Total * spell.SpellData.Coefficient2;
            var damage = 15 + (spell.CastInfo.SpellLevel * 20) + ad + ap;

            var spellO = owner.GetSpell("ZedShuriken");
            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false, spellO);
            AddParticle(owner, target, "zed_q_tar.troy", target.Position);
        }
    }
}