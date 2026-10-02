using System;
using System.Numerics;
using System.Collections.Generic;
using System.Activities.Presentation.View;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Handlers
{
    /// <summary>
    /// Class which calls collision based functions for GameObjects.
    /// </summary>
    public class CollisionHandler
    {
        private MapScriptHandler _map;
        public Movement.ActorSpatialIndex Actors { get; } = new();
        private readonly List<GameObject> _objects = new List<GameObject>();
        private QuadTree<GameObject> _quadDynamic;
        private readonly List<(GameObject Object, Circle Bounds)> _indexedBounds = new();
        private readonly List<(GameObject Object, Circle Bounds)> _nextBounds = new();
        private bool _treeDirty = true;
        private readonly List<GameObject> _updateScratch =
    new List<GameObject>();
        private readonly Dictionary<GameObject, (Vector2 Position, float Radius)> _navigationBlockers =
            new Dictionary<GameObject, (Vector2, float)>();
        public CollisionHandler(MapScriptHandler map)
        {
            _map = map;

            // Initializes a dynamic map using NavigationGrid properties and a CollisionObject which takes into account an object's CollisionRadius (+1 for insurance).
            // It will contain all GameObjects that should be able to collide with eachother, refer to IsCollisionObject.
            _quadDynamic = new QuadTree<GameObject>(
                _map.NavigationGrid.MinGridPosition.X, // MIN
                _map.NavigationGrid.MaxGridPosition.Z, // yep, MAX
                _map.NavigationGrid.MaxGridPosition.X -_map.NavigationGrid.MinGridPosition.X,
                _map.NavigationGrid.MaxGridPosition.Z - _map.NavigationGrid.MinGridPosition.Z
            );
        }

        /// <summary>
        /// Whether or not the specified GameObject is able to collide with other GameObjects.
        /// </summary>
        /// <param name="obj">GameObject to check.</param>
        /// <returns>True/False.</returns>
        private bool IsCollisionObject(GameObject obj)
        {
            // CollisionObjects can be any AI units, ObjBuildings, pure AttackableUnits, and pure GameObjects.
            return !(obj.IsToRemove() || obj is LevelProp || obj is Particle || obj is SpellMissile || obj is Region) && obj.CollisionRadius >= 0;
        }

        /// <summary>
        /// Whether or not the specified GameObject is affected by collisions with other GameObjects.
        /// Used to determine if OnCollision functions should be called.
        /// </summary>
        /// <param name="obj">GameObject to check.</param>
        /// <returns>True/False.</returns>
        private bool IsCollisionAffected(GameObject obj)
        {
            // Collision affected GameObjects are non-turret AI units, AttackableUnits, missiles, and pure GameObjects.
            return !(obj.IsToRemove() || obj is LevelProp || obj is Particle || obj is ObjBuilding || obj is BaseTurret);
        }

        Circle GetBounds(GameObject obj)
        {
            return new Circle(obj.Position, Math.Max(0.5f, obj.CollisionRadius));
        }

        /// <summary>
        /// Adds the specified GameObject to the list of GameObjects to check for collisions. *NOTE*: Will fail to fully add the GameObject if it is out of the map's bounds.
        /// </summary>
        /// <param name="obj">GameObject to add.</param>
        public void AddObject(GameObject obj)
        {
            _treeDirty = true;
            if (obj is ObjAIBase actor) Actors.Update(actor);
            bool collides = IsCollisionObject(obj);
            bool detects = IsCollisionAffected(obj);

            if (collides || detects)
            {
                _objects.Add(obj);
            }

            if (collides)
            {
                _quadDynamic.Insert(
                    obj,
                    GetBounds(obj)
                );
            }

            UpdateNavigationBlocker(obj);
        }

        /// <summary>
        /// GameObject to remove from the list of GameObjects to check for collisions.
        /// </summary>
        /// <param name="obj">GameObject to remove.</param>
        /// <returns>true if item is successfully removed; false otherwise.</returns>
        public bool RemoveObject(GameObject obj)
        {
            _treeDirty = true;
            if (obj is ObjAIBase actor) Actors.Remove(actor);
            if (_navigationBlockers.Remove(obj, out var footprint))
            {
                _map.NavigationGrid.RemoveRuntimeBlocker(
                    footprint.Position,
                    footprint.Radius
                );
            }

            return _objects.Remove(obj);
        }

        /// <summary>
        /// Gets the nearest GameObjects to the given GameObject.
        /// </summary>
        /// <param name="obj">GameObject which will be the origin of the check.</param>
        /// <returns>List of GameObjects. Null if GameObject is not present in the QuadTree.</returns>
        public List<GameObject> GetNearestObjects(GameObject obj)
        {
            return GetNearestObjects(GetBounds(obj));
        }

        /// <summary>
        /// Gets the nearest GameObjects to the given temporary circle object.
        /// </summary>
        /// <param name="circle"></param>
        /// <returns>List of GameObjects.</returns>
        public List<GameObject> GetNearestObjects(Circle circle)
        {
            var nearest = new List<GameObject>();

            foreach (var obj in _quadDynamic.GetNodesInside(circle))
            {
                nearest.Add(obj);
            }

            return nearest;
        }

        /// <summary>
        /// Function called every tick of the game by Map.cs.
        /// </summary>
        public void Update()
        {
            UpdateQuadTree();
            int count = _objects.Count;
            var objectsCopy = System.Buffers.ArrayPool<GameObject>.Shared.Rent(count);
            _objects.CopyTo(objectsCopy);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var obj = objectsCopy[i];
                    UpdateNavigationBlocker(obj);
                    if (obj is ObjAIBase actor) Actors.Update(actor);
                    UpdateCollision(obj);
                }
            }
            finally { System.Buffers.ArrayPool<GameObject>.Shared.Return(objectsCopy, clearArray: true); }

        }
        /// <summary>
        /// Updates collision for the specified object.
        /// </summary>
        /// <param name="obj">GameObject to check if colliding with other objects.</param>
        public void UpdateCollision(GameObject obj)
        {
            if (!IsCollisionAffected(obj))
            {
                return;
            }

            if (!_map.PathingHandler.IsWalkable(obj.Position))
            {
                obj.OnCollision(null, true);
            }

            
            // Don't allocate a temporary nearest object List here.
             
            foreach (var obj2 in
                     _quadDynamic.GetNodesInside(GetBounds(obj)))
            {
                if (obj == obj2)
                {
                    continue;
                }

                if (obj2.IsToRemove())
                {
                    continue;
                }

                if (obj.IsCollidingWith(obj2))
                {
                    obj.OnCollision(obj2);
                }
            }
        }

        /// <summary>
        /// Used to reinitialize a QuadTree's sectors when objects may have moved out of sectors, which makes them unable to be removed.
        /// </summary>
        private void UpdateQuadTree()
        {
            _nextBounds.Clear();
            bool changed = _treeDirty;
            foreach (var obj in _objects)
            {
                if (!IsCollisionObject(obj)) continue;
                var bounds = GetBounds(obj);
                int index = _nextBounds.Count;
                if (index >= _indexedBounds.Count || _indexedBounds[index].Object != obj
                    || _indexedBounds[index].Bounds.Position != bounds.Position
                    || _indexedBounds[index].Bounds.Radius != bounds.Radius)
                    changed = true;
                _nextBounds.Add((obj, bounds));
            }
            if (!changed && _nextBounds.Count == _indexedBounds.Count) return;
            _quadDynamic.Clear();
            foreach (var entry in _nextBounds)
                _quadDynamic.Insert(entry.Object, entry.Bounds);
            _indexedBounds.Clear();
            _indexedBounds.AddRange(_nextBounds);
            _treeDirty = false;
        }
        public void GetAIUnitsInRadius(
    Vector2 center,
    float radius,
    List<ObjAIBase> result)
        {
            Actors.Query(center, radius, result);
        }

        public List<ObjAIBase> GetAIUnitsInRadius(
            Vector2 center,
            float radius)
        {
            var result = new List<ObjAIBase>();

            GetAIUnitsInRadius(
                center,
                radius,
                result
            );

            return result;
        }
        private bool ShouldBlockNavigation(GameObject obj)
        {
            if (obj == null
                || obj.IsToRemove()
                || GetNavigationBlockerRadius(obj) <= 0.0f)
            {
                return false;
            }

            if (obj is BaseTurret turret)
            {
                return !turret.IsDead;
            }

            if (obj is ObjBuilding building)
            {
                return !building.IsDead;
            }

            return false;
        }

        private void UpdateNavigationBlocker(GameObject obj)
        {
            if (obj == null) return;
            bool shouldBlock = ShouldBlockNavigation(obj);
            bool isBlocking = _navigationBlockers.TryGetValue(obj, out var previous);
            float radius = GetNavigationBlockerRadius(obj);
            var footprint = (obj.Position, radius);

            // Remove the registered footprint, even if position/radius changed.
            if (isBlocking && (!shouldBlock || previous != footprint))
            {
                _map.NavigationGrid.RemoveRuntimeBlocker(previous.Position, previous.Radius);
                _navigationBlockers.Remove(obj);
                isBlocking = false;
            }

            if (shouldBlock && !isBlocking)
            {
                _map.NavigationGrid.AddRuntimeBlocker(
                    obj.Position,
                    radius
                );

                _navigationBlockers.Add(obj, footprint);
            }
        }

        private static float GetNavigationBlockerRadius(GameObject obj)
        {
            return obj is BaseTurret ? 125.0f : obj.PathfindingRadius;
        }
    }
}
