using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    [ProtoContract]
    public class TeleportList
    {
        [ProtoMember(1)]
        [SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Used by ProtoBuf")]
        private Dictionary<BlockPos, Teleport> _teleports = [];

        public event Action? Changed;
        public event Action<Teleport>? ValueChanged;
        public event Action<BlockPos>? ValueRemoved;

        public int Count => _teleports.Count;

        private readonly object _pointsLock = new();

        public Teleport? this[BlockPos pos]
        {
            get
            {
                lock (_pointsLock)
                {
                    if (_teleports.TryGetValue(pos, out Teleport? value))
                    {
                        return value;
                    }
                    return null;
                }
            }
            set
            {
                if (value != null)
                {
                    lock (_pointsLock)
                    {
                        if (_teleports.ContainsKey(pos))
                        {
                            _teleports[pos] = value;
                        }
                        else
                        {
                            _teleports.Add(value.Pos, value);
                        }
                    }

                    ValueChanged?.Invoke(value);
                    Changed?.Invoke();
                }
                else
                {
                    throw new ArgumentNullException(nameof(value));
                }
            }
        }

        public IReadOnlyList<Teleport> GetAll(Predicate<Teleport>? predicate = null)
        {
            lock (_pointsLock)
            {
                var list = _teleports.Values.ToList();
                return predicate == null ? list : list.FindAll(predicate);
            }
        }

        public void Set(Teleport teleport)
        {
            this[teleport.Pos] = teleport;
        }

        public bool Remove(BlockPos pos)
        {
            var removed = false;
            lock (_pointsLock)
            {
                removed = _teleports.Remove(pos);
            }
            if (removed)
            {
                ValueRemoved?.Invoke(pos);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public bool Contains(BlockPos pos)
        {
            lock (_pointsLock)
            {
                return _teleports.ContainsKey(pos);
            }
        }

        public void MarkDirty(BlockPos pos)
        {
            var value = (Teleport?)null;
            lock (_pointsLock)
            {
                value = this[pos];
            }
            if (value != null)
            {
                ValueChanged?.Invoke(value);
            }
        }

        public void SetFrom(TeleportList points)
        {
            lock (_pointsLock)
            {
                _teleports.Clear();
                foreach (var (pos, teleport) in points._teleports)
                {
                    _teleports.Add(pos, teleport);
                }
            }
            Changed?.Invoke();
        }

        public void SetFrom(IEnumerable<Teleport> points)
        {
            lock (_pointsLock)
            {
                _teleports.Clear();
                foreach (var teleport in points)
                {
                    _teleports.Add(teleport.Pos, teleport);
                }
            }
            Changed?.Invoke();
        }

        public TeleportList ForPlayer(IServerPlayer player)
        {
            lock (_pointsLock)
            {
                var list = new TeleportList();
                foreach (var teleport in _teleports)
                {
                    list[teleport.Key] = teleport.Value.ForPlayer(player.PlayerUID);
                }
                return list;
            }
        }
    }
}
