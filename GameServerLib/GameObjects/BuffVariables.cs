using System;
using System.Collections.Generic;

namespace LeagueSandbox.GameServer.GameObjects
{
    public sealed class BuffVariables
    {
        private readonly Dictionary<string, object> _values;

        public BuffVariables() : this(null) { }

        public BuffVariables(IDictionary<string, object> values)
        {
            _values = values != null
                ? new Dictionary<string, object>(values, StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, object> Values => _values;

        public object this[string key]
        {
            get => _values[key];
            set => _values[key] = value;
        }

        public void Set(string key, object value)
        {
            _values[key] = value;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_values.TryGetValue(key, out var raw))
            {
                if (raw is T typed)
                {
                    value = typed;
                    return true;
                }
                try
                {
                    value = (T)Convert.ChangeType(raw, typeof(T));
                    return true;
                }
                catch
                {
                    // ignored
                }
            }
            value = default;
            return false;
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            return TryGet<T>(key, out var value) ? value : defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            return TryGet<float>(key, out var value) ? value : defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            return TryGet<int>(key, out var value) ? value : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            return TryGet<bool>(key, out var value) ? value : defaultValue;
        }

        public BuffVariables Clone()
        {
            return new BuffVariables(_values);
        }
    }
}
