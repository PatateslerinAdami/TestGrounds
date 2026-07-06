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
        private Spell _spell;
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
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _target = target;
            _spell = spell;
        }
        
        public void OnSpellPostCast(Spell spell)
        {
            _fiora.CancelAutoAttack(true);
            if (!_fiora.HasBuff("FioraQCD")) { AddBuff("FioraQCD", 4, 1, spell, _fiora, _fiora); }
            SpellCast(_fiora, 0, SpellSlotType.ExtraSlots, false, _target, Vector2.Zero);
        }
    }

    public class FioraQLunge : ISpellScript
    {
        private float _damage;
        private ObjAIBase _fiora;
        private Spell _spell;
        private AttackableUnit _target;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _fiora = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _target = target;
            _spell = spell;
            _fiora.SetTargetUnit(null, true);
            SetStatus(_fiora, StatusFlags.Ghosted, true);
            _fiora.CancelAutoAttack(true);
        }
        
        public void OnSpellCast(Spell spell)
        {
            ApiEventManager.OnMoveEnd.AddListener(this, _fiora, OnMoveEnd, true);
            ApiEventManager.OnMoveSuccess.AddListener(this, _fiora, OnMoveSuccess, true);
            
            PlayAnimation(_fiora, "Spell1");
            AddParticleTarget(_fiora, _fiora, "Fiora_Dance_windup.troy", _fiora);
            AddParticleTarget(_fiora, _fiora, "FioraQLunge_dashtrail.troy", _fiora);
            ForceMove(_fiora, GetMovePositionByCollisionOffset(_fiora, _target,0), 2200, 0f, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, true, true, ForceMovementOrdersType.CANCEL_ORDER, "FioraQLunge");
        }
        
        // AICI ESTE REPARAȚIA: Am adăugat parametrul ForceMovementParameters
        private void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
        {
            parameters:
            var ad = _fiora.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
            _damage = 15 + (25f * _fiora.Spells[0].CastInfo.SpellLevel) + (_fiora.Stats.AttackDamage.FlatBonus * 1.2f);
            _target.TakeDamage(_fiora, _damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
            AddParticleTarget(_fiora, _target, "FioraQLunge_tar", _target);
            if (_fiora.Team != _target.Team && _target is Champion)
            {
                _fiora.SetTargetUnit(_target, true);
            }
        }
        
        // AICI ESTE REPARAȚIA: Am adăugat parametrul ForceMovementParameters
        private void OnMoveEnd(AttackableUnit owner, ForceMovementParameters parameters)
        {
            SetStatus(_fiora, StatusFlags.Ghosted, false);
            StopAnimation(_fiora, "spell1", StopAnimationFlags.StopAll | StopAnimationFlags.Fade | StopAnimationFlags.IgnoreLock);
        }
    }
}