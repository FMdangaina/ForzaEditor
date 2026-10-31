namespace ForzaStudio
{
    using System.Collections;
    using System.Collections.Generic;

    // A set (the project targets .NET 2.0, which has no HashSet).
    public sealed class Set<T> : IEnumerable<T>
    {
        private readonly Dictionary<T, bool> items;

        public Set()
        {
            this.items = new Dictionary<T, bool>();
        }

        public Set(IEnumerable<T> source) : this(source, null)
        {
        }

        public Set(IEnumerable<T> source, IEqualityComparer<T> comparer)
        {
            this.items = new Dictionary<T, bool>(comparer);
            foreach (T item in source)
            {
                this.items[item] = true;
            }
        }

        public int Count
        {
            get { return this.items.Count; }
        }

        public bool Add(T item)
        {
            if (this.items.ContainsKey(item))
            {
                return false;
            }
            this.items[item] = true;
            return true;
        }

        public bool Remove(T item)
        {
            return this.items.Remove(item);
        }

        public bool Contains(T item)
        {
            return this.items.ContainsKey(item);
        }

        public void Clear()
        {
            this.items.Clear();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.items.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.items.Keys.GetEnumerator();
        }
    }
}
