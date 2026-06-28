using System.Collections.Generic;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace LeagueSandbox.GameServer.Handlers
{
    /// <summary>
    /// Holds the map's <see cref="CapturePoint"/>s (Twisted Treeline altars / Dominion control points)
    /// and ticks them, mirroring Riot's <c>CapturePoint::CapturePointManager::Update</c>. Created per
    /// map by <see cref="MapScriptHandler"/> and ticked from its Update; capture points are registered
    /// by the content map script (via the map function API) once the altar units exist.
    /// </summary>
    public class CapturePointManager
    {
        private readonly Game _game;
        private readonly List<CapturePoint> _points = new List<CapturePoint>();

        public IReadOnlyList<CapturePoint> CapturePoints => _points;
        public bool HasCapturePoints => _points.Count > 0;

        public CapturePointManager(Game game)
        {
            _game = game;
        }

        /// <summary>
        /// Registers a capture point on the given altar unit. The altar's PrimaryAbilityResource (mana,
        /// max = <paramref name="goal"/>) is used as the replicated capture meter. Returns the
        /// <see cref="CapturePoint"/> so the caller can subscribe to OnCaptured/OnUnlocked.
        /// </summary>
        public CapturePoint AddCapturePoint(AttackableUnit altar, float goal, float neutralValue, float fillRate,
            float decayRate, float lockDuration, float captureRadius, float unlockTime)
        {
            var point = new CapturePoint(_game, altar, goal, neutralValue, fillRate, decayRate, lockDuration, captureRadius, unlockTime);
            _points.Add(point);
            return point;
        }

        public void Update(float diff)
        {
            foreach (var point in _points)
            {
                point.Update(diff);
            }
        }
    }
}
