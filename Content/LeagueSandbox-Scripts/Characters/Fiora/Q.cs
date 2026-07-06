using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FioraQ : ISpellScript
    {
        private ObjAIBase _fiora;
        private AttackableUnit _target;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = false,
            DoesntBreakShields = true,
            TriggersSpellCasts = true,
            CastingBreaksStealth = true,
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _fiora = owner;
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        }

        private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
        {
            AddParticleTarget(_fiora, _target, "FioraQLunge_tar", _target, flags: FXFlags.SimulateWhileOffScreen);
            var ad = _fiora.Stats.AttackDamage.FlatBonus * spell.SpellData.Coefficient; 
            var modifier = _fiora.HasBuff("FioraQCD") ? 1f : 2f;
            var damage = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ad * modifier;
            _target.TakeDamage(_fiora, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                false);
            
            if (_target is Champion)
            {
                _fiora.SetTargetUnit(_target, true);
            }
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _target = target;
        }

        public void OnSpellPostCast(Spell spell)
        {
            _fiora.CancelAutoAttack(true);
            if (!_fiora.HasBuff("FioraQCD"))
            {
                AddBuff("FioraQCD", 4f, 1, spell, _fiora, _fiora);
            }
            else
            {
                RemoveBuff(_fiora, "FioraQCD");
            }

            SpellCast(_fiora, 0, SpellSlotType.ExtraSlots, true, _target, Vector2.Zero);
        }
    }

    public class FioraQLunge : ISpellScript
    {
        private ObjAIBase _fiora;
        private AttackableUnit _target;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDeathRecapSource = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _fiora = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _target = target;
            ApiEventManager.OnMoveEnd.AddListener(this, _fiora, OnMoveEnd, true);
            ApiEventManager.OnMoveSuccess.AddListener(this, _fiora, OnMoveSuccess, true);
            FaceDirection(_target.Position, _fiora, true);
            PlayAnimation(_fiora, "Spell1", flags: AnimationFlags.Junk6 | AnimationFlags.Junk7);
            AddParticleTarget(_fiora, _fiora, "FioraQLunge_dashtrail.troy", _fiora, flags: FXFlags.SimulateWhileOffScreen);
            ForceMove(_fiora, GetMovePositionByCollisionOffset(_fiora, _target, 0), 2200, 0f,
                ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, true, true,
                ForceMovementOrdersType.CANCEL_ORDER, "FioraQLunge");
        }
        
        private void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters.MovementName != "FioraQLunge") return;
            var mainSpell = _fiora.Spells[0];
            mainSpell.ApplyEffects(_target);
            
        }
        
        private void OnMoveEnd(AttackableUnit owner, ForceMovementParameters parameters)
        {
            if (parameters.MovementName != "FioraQLunge") return;
            StopAnimation(_fiora, "spell1",
               StopAnimationFlags.FadeOut | StopAnimationFlags.IgnoreLock);
        }
    }
}