using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;
using ShacoScripts;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using ClonePet = LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Pet;

namespace AIScripts
{
    public class ShacoClone : IAIScript
    {
        private ClonePet _clone;
        private float _scanTimer;
        private bool _holdPosition;
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData
        {
            HandlesTargetAcquisition = true
        };

        public void OnActivate(ObjAIBase owner)
        {
            _clone = (ClonePet)owner;
            _clone.UsesClientAttackLoop = true;
            _clone.KeepChasingDuringAttackCooldown = true;
            ApiEventManager.OnUnitUpdateMoveOrder.AddListener(this, owner, OnOrder);
        }

        private bool OnOrder(ObjAIBase owner, OrderType order)
        {
            if (order == OrderType.PetHardMove) _holdPosition = true;
            if (order == OrderType.PetHardAttack || order == OrderType.PetHardReturn) _holdPosition = false;

            if (order == OrderType.PetHardAttack) owner.SetAIState(AIState.AI_PET_HARDATTACK);
            if (order == OrderType.PetHardMove) owner.SetAIState(AIState.AI_PET_HARDMOVE);
            if (order == OrderType.PetHardReturn) owner.SetAIState(AIState.AI_PET_HARDRETURN);
            if (order == OrderType.PetHardStop) owner.SetAIState(AIState.AI_PET_HARDSTOP);
            return true;
        }

        public void OnUpdate(float diff)
        {
            if (_clone.IsDead || _clone.IsToRemove() || _clone.Owner == null) return;
            _scanTimer -= diff;
            if (_scanTimer > 0) return;
            _scanTimer = 150;

            if (!_clone.CanIssueMoveOrders()) return;
            float ownerDistance = Vector2.DistanceSquared(_clone.Position, _clone.Owner.Position);
            if (ownerDistance > 2000 * 2000)
            {
                _clone.SetTargetUnit(null, true);
                _clone.TeleportTo(_clone.Owner.Position);
                _clone.UpdateMoveOrder(OrderType.Hold, true);
            }

            if (_clone.MoveOrder == OrderType.PetHardStop) return;
            if (_clone.TargetUnit != null)
            {
                if (ShacoHelpers.CanAttack(_clone, _clone.TargetUnit)) return;
                _clone.SetTargetUnit(null, true);
                _clone.StopMovement();
                _clone.UpdateMoveOrder(_holdPosition ? OrderType.PetHardMove : OrderType.Hold, true);
            }

            if (_clone.MoveOrder == OrderType.PetHardReturn)
            {
                FollowOwner(ownerDistance);
                return;
            }
            if (_clone.MoveOrder == OrderType.PetHardMove && !_clone.IsPathEnded()) return;

            var enemies = GetUnitsInRange(_clone, _clone.Position, 475, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions);
            var target = enemies.FirstOrDefault(unit => unit == _clone.Owner.TargetUnit && ShacoHelpers.CanAttack(_clone, unit))
                ?? enemies.FirstOrDefault(unit => ShacoHelpers.CanAttack(_clone, unit));
            if (target != null)
            {
                _clone.SetTargetUnit(target, true);
                if (!_holdPosition) _clone.UpdateMoveOrder(OrderType.AttackTo, true);
                return;
            }

            if (!_holdPosition) FollowOwner(ownerDistance);
        }

        private void FollowOwner(float distanceSquared)
        {
            if (distanceSquared <= _clone.GetReturnRadius() * _clone.GetReturnRadius())
            {
                _clone.StopMovement();
                _clone.UpdateMoveOrder(OrderType.Hold, true);
                return;
            }
            var path = GetPath(_clone.Position, _clone.Owner.Position, _clone.PathfindingRadius);
            if (path == null || path.Count < 2) return;
            _clone.SetWaypoints(path);
            if (_clone.MoveOrder != OrderType.PetHardReturn)
            {
                _clone.UpdateMoveOrder(OrderType.PetHardReturn, true);
            }
        }
    }
}
