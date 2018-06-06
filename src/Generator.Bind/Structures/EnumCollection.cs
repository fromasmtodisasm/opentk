using System.Collections.Generic;
using System.Linq;

namespace Bind.Structures
{
    /// <summary>
    /// Holds a collection of enumeration definitions.
    /// </summary>
    internal class EnumCollection : IDictionary<string, EnumDefinition>
    {
        private SortedDictionary<string, EnumDefinition> _enumerations = new SortedDictionary<string, EnumDefinition>();

        // Return -1 for ext1, 1 for ext2 or 0 if no preference.
        private int OrderOfPreference(string ext1, string ext2)
        {
            // If one is empty and the other not, prefer the empty one (empty == core)
            // Otherwise check for Arb and Ext. To reuse the logic for the
            // empty check, let's try to remove first Arb, then Ext from the strings.
            int ret = PreferEmpty(ext1, ext2);
            if (ret != 0)
            {
                return ret;
            }

            ext1 = ext1.Replace("Arb", string.Empty);
            ext2 = ext2.Replace("Arb", string.Empty);
            ret = PreferEmpty(ext1, ext2);
            if (ret != 0)
            {
                return ret;
            }

            ext1 = ext1.Replace("Ext", string.Empty);
            ext2 = ext2.Replace("Ext", string.Empty);
            return PreferEmpty(ext1, ext2);
        }

        // Prefer the empty string over the non-empty.
        private int PreferEmpty(string ext1, string ext2)
        {
            if (string.IsNullOrEmpty(ext1) && !string.IsNullOrEmpty(ext2))
            {
                return -1;
            }

            if (string.IsNullOrEmpty(ext2) && !string.IsNullOrEmpty(ext1))
            {
                return 1;
            }

            return 0;
        }

        /// <inheritdoc/>
        public void Add(string key, EnumDefinition value)
        {
            if (ContainsKey(key))
            {
                var e = this[key];
                foreach (var token in value.ConstantCollection.Values)
                {
                    Utilities.Merge(e, token);
                }
            }
            else
            {
                _enumerations.Add(key, value);
            }
        }

        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            return _enumerations.ContainsKey(key);
        }

        /// <inheritdoc/>
        public ICollection<string> Keys => _enumerations.Keys;

        /// <inheritdoc/>
        public bool Remove(string key)
        {
            return _enumerations.Remove(key);
        }

        /// <inheritdoc/>
        public bool TryGetValue(string key, out EnumDefinition value)
        {
            return _enumerations.TryGetValue(key, out value);
        }

        /// <inheritdoc/>
        public ICollection<EnumDefinition> Values => _enumerations.Values;

        /// <inheritdoc/>
        public EnumDefinition this[string key]
        {
            get => _enumerations[key];
            set => _enumerations[key] = value;
        }

        /// <inheritdoc/>
        public void Add(KeyValuePair<string, EnumDefinition> item)
        {
            _enumerations.Add(item.Key, item.Value);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _enumerations.Clear();
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, EnumDefinition> item)
        {
            return _enumerations.Contains(item);
        }

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, EnumDefinition>[] array, int arrayIndex)
        {
            _enumerations.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        public int Count => _enumerations.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => (_enumerations as IDictionary<string, EnumDefinition>).IsReadOnly;

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, EnumDefinition> item)
        {
            return _enumerations.Remove(item.Key);
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, EnumDefinition>> GetEnumerator()
        {
            return _enumerations.GetEnumerator();
        }

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _enumerations.GetEnumerator();
        }
    }
}
