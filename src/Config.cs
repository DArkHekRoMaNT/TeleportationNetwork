namespace TeleportationNetwork
{
    public class Config
    {
        public static Config Current { get; set; }
        public class Part<T>
        {
            public readonly string Comment;
            public readonly T Default;
            private T val;
            public T Val
            {
                get => (val != null ? val : val = Default);
                set => val = (value != null ? value : Default);
            }
            public Part(T Default, string Comment = null)
            {
                this.Default = Default;
                this.Val = Default;
                this.Comment = Comment;
            }
            public Part(T Default, string prefix, string[] allowed, string postfix = null)
            {
                this.Default = Default;
                this.Val = Default;
                this.Comment = prefix;

                this.Comment += "[" + allowed[0];
                for (int i = 1; i < allowed.Length; i++)
                {
                    this.Comment += ", " + allowed[i];
                }
                this.Comment += "]" + postfix;
            }
        }

        public Part<bool> SharedTeleports { get; set; } = new Part<bool>(false, "All activated teleport to all players");
        public Part<bool> CreateClaim { get; set; } = new Part<bool>(false);
        public Part<bool> Unbreakable { get; set; } = new Part<bool>(true);
    }


    // public class UIConfig
    // {
    //     public static UIConfig Current { get; set; }

    //     public class Part<T> : IDictionary where T : unmanaged
    //     {
    //         public string Desc { get; set; }
    //         private Dictionary<T, bool> _selected;

    //         #region IDictionary

    //         public object this[object key] { get => ((IDictionary)_selected)[key]; set => ((IDictionary)_selected)[key] = value; }

    //         public ICollection Keys => ((IDictionary)_selected).Keys;

    //         public ICollection Values => ((IDictionary)_selected).Values;

    //         public bool IsReadOnly => ((IDictionary)_selected).IsReadOnly;

    //         public bool IsFixedSize => ((IDictionary)_selected).IsFixedSize;

    //         public int Count => ((ICollection)_selected).Count;

    //         public object SyncRoot => ((ICollection)_selected).SyncRoot;

    //         public bool IsSynchronized => ((ICollection)_selected).IsSynchronized;

    //         public void Add(object key, object value)
    //         {
    //             ((IDictionary)_selected).Add(key, value);
    //         }

    //         public void Clear()
    //         {
    //             ((IDictionary)_selected).Clear();
    //         }

    //         public bool Contains(object key)
    //         {
    //             return ((IDictionary)_selected).Contains(key);
    //         }

    //         public void CopyTo(Array array, int index)
    //         {
    //             ((ICollection)_selected).CopyTo(array, index);
    //         }

    //         public IEnumerator GetEnumerator()
    //         {
    //             return ((IEnumerable)_selected).GetEnumerator();
    //         }

    //         public void Remove(object key)
    //         {
    //             ((IDictionary)_selected).Remove(key);
    //         }

    //         IDictionaryEnumerator IDictionary.GetEnumerator()
    //         {
    //             return ((IDictionary)_selected).GetEnumerator();
    //         }

    //         #endregion
    //     }
    // }
}