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
    public class ThreshQ : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true,
            AutoFaceDirection = false
        };
        public ObjAIBase _owner;
        Vector2 _start, _end;
        AttackableUnit _target;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell, true);
        }
        private void OnLevelUpSpell(Spell spell)
        {
            SetSpell(_owner, "ThreshQInternal", SpellSlotType.ExtraSlots, 9, true);
        }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _start = start;
            _end = end;
            _target = target;
        }
        public void OnSpellPostCast(Spell spell)
        {
            _owner.GetSpell("ThreshQInternal").Cast(_start, _end, _target);
        }
    }
    public class ThreshQInternal : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };
        public ObjAIBase _owner;
        Vector2 _start, _end;
        AttackableUnit _target;
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _owner = owner;
            _start = start;
            _end = end;
            _target = target;
        }
        public void OnSpellPostCast(Spell spell)
        {
            SpellCast(_owner, 0, SpellSlotType.ExtraSlots, _end, _end, true, Vector2.Zero);
        }
    }
    public class ThreshQMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle,
            },
            IsDamagingSpell = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile, false);
        }

        public void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
        }

        public void OnMissileEnd(SpellMissile missile)
        {
            if (missile is SpellCircleMissile circleMissile && circleMissile.ObjectsHit.Count == 0)
            {
                missile.CastInfo.Owner.StopAnimation("Spell1_IN", fade: true);
            }
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            target.TakeDamage(owner, 80f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);

            AddParticleTarget(owner, target, "Thresh_Q_Pull_Sound.troy", target);
            AddParticleTarget(owner, target, "Thresh_Q_stab_tar.troy", target);
            AddParticleTarget(owner, target, "Thresh_Q_whip_pull_beam.troy", owner, 1.5f, bone: "head", targetBone: "R_hand");
            AddBuff("ThreshQ", 1.5f, 1, spell, target, owner);
            AddUnitPerceptionBubble(target, 1f, 1.6f, owner.Team, false, target, ignoresLoS: true, onlyShowTarget: true);
            var spellA = owner.SetSpell("ThreshQLeap", 0, true);
            if (spellA.Script is ThreshQLeap tl)
            {
                tl.a = target;
            }
            PlayAnimation(owner, "Spell1_GRAB", timeScale: 1f);
            owner.RegisterTimer(new GameScriptTimer(0.1f, () =>
            {
                StopAnimation(owner, "Spell1_GRAB", fade: true);
                if (!target.IsDead && owner.GetSpell("ThreshQLeap") != null)
                {
                    PlayAnimation(owner, "Spell1_PULL1_UpB", timeScale: 1.0f);
                    PerformTug(owner, target);
                }
            }));
            owner.RegisterTimer(new GameScriptTimer(0.7f, () =>
            {
                if (!target.IsDead && owner.GetSpell("ThreshQLeap") != null)
                {
                    PlayAnimation(owner, "Spell1_PULL2_UpB", timeScale: 1.0f);
                    PerformTug(owner, target, true);
                }
            }));
            owner.RegisterTimer(new GameScriptTimer(1.5f, () =>
            {
                if (owner.Spells[0].SpellName == "ThreshQLeap")
                {
                    owner.SetSpell("ThreshQ", 0, true);
                }
            }));

            missile.SetToRemove();
        }

        private void PerformTug(ObjAIBase owner, AttackableUnit target, bool secondTug = false)
        {
            var dir = Vector2.Normalize(owner.Position - target.Position);
            var distance = Vector2.Distance(owner.Position, target.Position);
            if ((distance > 300f && secondTug) || !secondTug)
            {
                var pullPosition = target.Position + (dir * 150f);
                target.DashToLocation(pullPosition, 1000f, "RUN", consideredCC: true);
            }
        }
    }

    public class ThreshQLeap : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true
        };

        public AttackableUnit a;

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var hookedTarget = a;
            var reg = AddUnitPerceptionBubble(a, 1f, 16f, owner.Team, false, a, ignoresLoS: true, onlyShowTarget: true);
            //StopAnimation(owner, "Spell1_PULL1_UpB", false, false, true); 
            //StopAnimation(owner, "Spell1_PULL2_UpB", false, false, true); 
            StopAnimation(owner, "Spell1_PULL1", false, false, true);
            StopAnimation(owner, "Spell1_PULL2", false, false, true);
            StopAnimation(owner, "Spell1_GRAB", false, false, true);
            if (hookedTarget != null && Vector2.Distance(owner.Position, hookedTarget.Position) <= 3000f)
            {

                owner.DashToTarget(hookedTarget, 1000f, "Spell1_Dash", consideredCC: false, keepFacingLastDirection: false);
                ApiEventManager.OnMoveEnd.AddListener(this, owner, (unit, movementParams) =>
                {
                    if (reg != null && !reg.IsToRemove())
                    {
                        reg.SetToRemove();
                    }
                }, true);

            }
            owner.SetSpell("ThreshQ", 0, true);
        }
    }
}