using GameMaths;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class AatroxQ : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        ObjAIBase _owner;
        Spell _spell;
        Vector2 endPos2D;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
        }
        public void OnSpellCast(Spell spell)
        {
            endPos2D = new Vector2(_spell.CastInfo.TargetPosition.X, _spell.CastInfo.TargetPosition.Z);
            PlayAnimation(_owner, "Spell1", 1f);
            AddParticle(_owner, null, "Aatrox_Base_Q_Tar_Green.troy", endPos2D, teamOnly: _owner.Team);
            AddParticle(_owner, null, "Aatrox_Base_Q_Tar_Red.troy",   endPos2D, teamOnly: CustomConvert.GetEnemyTeam(_owner.Team));
            Jump();
            _owner.RegisterTimer(new GameScriptTimer(0.3f, () =>
            {
                Dash();
            }));
        }

        private void Jump()
        {
            _owner.StopMovement(networked: false);
            FaceDirection(endPos2D, _owner, true);
            // eh since i tested out a CustomDashTest for some visual thing and not integrated it to forcemovement other things stopped by the forcemovement like having target to auto attack visually break the ascend
            _owner.SetStatus(StatusFlags.CanMove, false);
            Vector2 direction = new Vector2(_owner.Direction.X, _owner.Direction.Z);

            float jumpDistance = 10f;
            Vector2 jumpTarget = _owner.Position + (direction * jumpDistance);
            Vector2 jumpTargetFal = _owner.Position - (direction * jumpDistance);

            float jumpTime = 5f;
            float jumpSpeed = jumpDistance / (jumpTime);
            float jumpGravity = 4f;

            CustomDashTest(_owner, jumpTarget, jumpSpeed, jumpGravity, jumpTargetFal);
        }
        private void Dash()
        {
            _owner.SetStatus(StatusFlags.CanMove, true);
            var distance = (endPos2D - _owner.Position).Length();
            var speed = distance / 0.3f;
            // at some point we should add unstoppable dashes
            _owner.DashToLocation(endPos2D, speed, leapGravity: 0f, movementName:"AatroxQDash");

            ApiEventManager.OnMoveSuccess.AddListener(this, _owner, OnMoveSuccess, true);

        }
        public void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters.MovementName != "AatroxQDash") return;

            StopAnimation(_owner, "Spell1", fade: true);
            AddParticle(_owner, null, "Aatrox_Base_Q_Land", _owner.Position);

            var units = GetUnitsInRangeDiffTeam(_owner.Position, 75, true, _owner);
            foreach (var u in units)
            {
                if(_spell.SpellData.IsValidTarget(_owner, u))
                {
                    u.DashToLocation(new Vector2(u.Position.X + 5f, u.Position.Y + 5f), 10, "Run", 10f);
                    AddParticleTarget(_owner, u, "Aatrox_Base_Q_Hit.troy", u);
                }
            }
        }
    }
}