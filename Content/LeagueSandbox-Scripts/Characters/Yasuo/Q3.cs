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
    public class YasuoQ3W : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CooldownIsAffectedByCDR = false
        };

        public float? GetCooldown(Spell spell, byte rank)
        {
            return YasuoCastHelpers.QCooldown(spell, rank);
        }

        public ObjAIBase _owner;
        Vector2 _start, _end;
        AttackableUnit _target;

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            YasuoCastHelpers.SyncQRank(owner, spell);
            _owner = owner;
            _start = start;
            _end = end;
            _target = target;
            YasuoCastHelpers.ResetSheath(spell);
        }

        public void OnSpellPostCast(Spell spell)
        {
            if (YasuoCastHelpers.TryArmCombo(spell))
            {
                return;
            }

            _owner.GetSpell("YasuoQ3").Cast(_start, _end, _target);
        }
    }

    public class YasuoQ3 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };

        private Buffs.YasuoDashController _dash;

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _dash = YasuoCastHelpers.ActiveDash(owner);
            ScriptMetadata.AutoFaceDirection = _dash == null;
            if (_dash == null)
            {
                FaceDirection(end, owner, true);
                YasuoCastHelpers.PrepareThrust(owner, spell);
            }
            else
            {
                spell.CastInfo.DesignerCastTime = spell.CastInfo.DesignerTotalTime = 0;
            }
        }

        public void OnSpellCast(Spell spell)
        {
            if (_dash == null)
            {
                YasuoCastHelpers.AddThrustVisuals(spell);
            }
        }

        public void OnSpellPostCast(Spell spell)
        {
            if (_dash != null)
            {
                _dash.ArmCombo(spell);
                return;
            }

            var owner = spell.CastInfo.Owner;
            var direction = YasuoCastHelpers.ThrustDirection(spell);
            var targetPos = owner.Position + new Vector2(direction.X, direction.Z) * 1100f;
            SpellCast(owner, 3, SpellSlotType.ExtraSlots, targetPos, targetPos, true, Vector2.Zero, isForceCastingOrChanneling: true);
            if (owner.HasBuff("YasuoQ3W"))
            {
                owner.RemoveBuffsWithName("YasuoQ3W");
            }
        }
    }

    public class YasuoQ3Mis : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle,
                ReplicationHeightOffset = 100,
            },
            IsDamagingSpell = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
            spell.SetOverrideCastRange(1100.0f);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            spell.CastInfo.Targets.Clear();
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            bool firstTarget = missile is not SpellCircleMissile circle || circle.ObjectsHit.Count <= 1;
            YasuoCastHelpers.ApplyQDamage(spell, target, firstTarget, missile?.CastInfo.SpellLevel);
            AddBuff(YasuoCastHelpers.KnockupBuffName(target), YasuoCastHelpers.KnockupDuration, 1, spell, target, owner);
            AddParticleTarget(owner, target, "yasuo_base_q_wind_hit_tar.troy", target);
        }
    }
}
