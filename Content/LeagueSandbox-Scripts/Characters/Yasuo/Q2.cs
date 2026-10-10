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
    public class YasuoQ2W : ISpellScript
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
            YasuoCastHelpers.ResetSheath(spell);
            _owner = owner;
            _start = start;
            _end = end;
            _target = target;
        }

        public void OnSpellPostCast(Spell spell)
        {
            if (YasuoCastHelpers.TryArmCombo(spell))
            {
                return;
            }

            _owner.GetSpell("YasuoQ2").Cast(_start, _end, _target);
        }
    }

    public class YasuoQ2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };

        bool FirstTarget = true;
        private Buffs.YasuoDashController _dash;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

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

            FirstTarget = true;
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
            spell.CreateSpellSector(new SectorParameters { BindObject = owner, DirectionOverride = YasuoCastHelpers.ThrustDirection(spell), Length = 450f, Width = 100f, PolygonVertices = new Vector2[] { new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, 0) }, SingleTick = true, Type = SectorType.Polygon });
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (sector?.Parameters.Type == SectorType.Area)
            {
                return;
            }

            var owner = spell.CastInfo.Owner;
            YasuoCastHelpers.ApplyQDamage(spell, target, FirstTarget);
            if (FirstTarget)
            {
                AddParticleTarget(owner, target, "Yasuo_Base_Q_hit_tar", target);
                FirstTarget = false;
                AddParticleTarget(owner, owner, "yasuo_q2_ready_buff.troy", owner, size: 1f, unitOnly: owner, reqVision: false);
                if (owner.HasBuff("YasuoQ"))
                {
                    owner.RemoveBuffsWithName("YasuoQ");
                }

                AddBuff("YasuoQ3W", 9f, 1, spell, owner, owner);
            }
        }
    }
}
