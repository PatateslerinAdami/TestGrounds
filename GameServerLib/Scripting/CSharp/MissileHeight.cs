using System;

namespace LeagueSandbox.GameServer.Scripting.CSharp
{
    public readonly struct MissileHeight
    {
        private readonly float _value;
        private readonly bool _absolute;

        private MissileHeight(float value, bool absolute)
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _value = value;
            _absolute = absolute;
        }

        public static MissileHeight Ground(float offset)
        {
            return new MissileHeight(offset, false);
        }

        public static MissileHeight World(float height)
        {
            return new MissileHeight(height, true);
        }

        public float Resolve(float groundHeight)
        {
            return _absolute ? _value : groundHeight + _value;
        }
    }
}
