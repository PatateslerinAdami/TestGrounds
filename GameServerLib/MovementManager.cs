using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.Movement;

namespace GameServerLib
{
    public class MovementManager
    {
        private const float MovementTickMs = 50.0f;
        private readonly Game _game;
        private float _accumulator;
        private bool _minionTick;
        private readonly List<ObjAIBase> _movementUnits = new();

        public ChampionMotionSolver Champions { get; }
        public AiPathMotionSolver AiPaths { get; }
        private readonly Queue<ObjAIBase> _pathQueue = new();
        private readonly HashSet<ObjAIBase> _queued = new();
        private readonly HashSet<ObjAIBase> _granted = new();
        private int _pathSearches;
        private bool _frameBegun;
        public void BeginFrame()
        {
            _pathSearches = 0; _frameBegun = true;
            _granted.Clear();
            while (_pathQueue.Count > 0 && _pathSearches < 16)
            {
                var actor = _pathQueue.Dequeue();
                _queued.Remove(actor);
                if (actor.IsDead || actor.IsToRemove()) continue;
                _granted.Add(actor);
                _pathSearches++;
            }
        }
        private void Step(ObjAIBase unit, float delta)
        {
            var state = unit.NavigationState;
            state.RequestedMovement = state.CandidateMovement = state.AcceptedMovement = System.Numerics.Vector2.Zero;
            unit.UpdatePathMovement(delta);
            _game.Map.CollisionHandler.Actors.Update(unit);
        }
        public bool TryReservePathSearch(ObjAIBase? actor = null)
        {
            if (actor != null && _granted.Remove(actor)) return true;
            if (_pathSearches < 16 && _pathQueue.Count == 0) { _pathSearches++; return true; }
            if (actor != null && _queued.Add(actor)) _pathQueue.Enqueue(actor);
            return false;
        }

        public MovementManager(Game game) { _game = game; Champions = new(game); AiPaths = new(game); }

        public void Update(float diff)
        {
            if (!float.IsFinite(diff) || diff <= 0) return;
            if (!_frameBegun) _pathSearches = 0;
            _frameBegun = false;
            _accumulator += diff;
            _game.ObjectManager.CollectMovementUnits(_movementUnits);
            foreach (var unit in _movementUnits)
                _game.Map.CollisionHandler.Actors.Update(unit);

            // Process catch up ticks in time order. Running every hero tick first
            // exposes future hero positions to all of the pending minion ticks.
            while (_accumulator >= MovementTickMs)
            {
                _accumulator -= MovementTickMs;
                _minionTick = !_minionTick;
                foreach (var unit in _movementUnits)
                    if (unit is not Minion) Step(unit, MovementTickMs);
                if (!_minionTick)
                {
                    foreach (var unit in _movementUnits)
                        if (unit is Minion) Step(unit, MovementTickMs * 2);
                }
            }
        }
    }
}
