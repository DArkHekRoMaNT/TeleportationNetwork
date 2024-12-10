using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace TeleportationNetwork
{
    /// <summary>
    /// Thread safe collection of teleports
    /// </summary>
    public class TeleportList : IEnumerable<Teleport>
    {
        private readonly Dictionary<BlockPos, Teleport> _points = [];
        private readonly object _pointsLock = new();

        public event Action? Changed;
        public event Action<Teleport>? ValueChanged;
        public event Action<BlockPos>? ValueRemoved;

        public int Count => _points.Count;

        public bool TryGetValue(BlockPos pos, [NotNullWhen(true)] out Teleport? value)
        {
            lock (_pointsLock)
            {
                return _points.TryGetValue(pos, out value);
            }
        }

        public bool AddOrUpdate(Teleport value)
        {
            lock (_pointsLock)
            {
                if (_points.ContainsKey(value.Pos))
                {
                    _points[value.Pos] = value;
                }
                else
                {
                    _points.Add(value.Pos, value);
                }
            }

            ValueChanged?.Invoke(value);
            Changed?.Invoke();
            return true;
        }

        public bool Remove(BlockPos pos)
        {
            var removed = false;
            lock (_pointsLock)
            {
                removed = _points.Remove(pos);
            }

            if (removed)
            {
                ValueRemoved?.Invoke(pos);
                Changed?.Invoke();
            }

            return removed;
        }

        public bool Contains(BlockPos pos)
        {
            lock (_pointsLock)
            {
                return _points.ContainsKey(pos);
            }
        }

        public void MarkDirty(BlockPos pos)
        {
            Teleport? value;
            lock (_pointsLock)
            {
                if (!_points.TryGetValue(pos, out value))
                {
                    return;
                }
            }
            ValueChanged?.Invoke(value);
            Changed?.Invoke();
        }

        public void SetFrom(Teleport[] points)
        {
            lock (_pointsLock)
            {
                _points.Clear();
                foreach (var teleport in points)
                {
                    _points.Add(teleport.Pos, teleport);
                }
            }
            Changed?.Invoke();
        }

        public void Link(BlockPos pos1, BlockPos pos2)
        {
            var changes = new HashSet<Teleport>();

            lock (_pointsLock)
            {
                if (!_points.TryGetValue(pos1, out var tp1))
                {
                    return;
                }

                if (!_points.TryGetValue(pos2, out var tp2))
                {
                    return;
                }

                changes.AddRange(UnlinkInternal(tp1));
                changes.AddRange(UnlinkInternal(tp2));

                tp1.Target = pos2;
                tp2.Target = pos1;

                changes.Add(tp1);
                changes.Add(tp2);
            }

            foreach (var tp in changes)
            {
                ValueChanged?.Invoke(tp);
            }
        }

        public void Unlink(BlockPos pos)
        {
            var changes = new HashSet<Teleport>();

            lock (_pointsLock)
            {
                if (_points.TryGetValue(pos, out var tp))
                    changes.AddRange(UnlinkInternal(tp));
            }

            foreach (var tp in changes)
            {
                ValueChanged?.Invoke(tp);
            }
        }

        public HashSet<Teleport> UnlinkInternal(Teleport teleport)
        {
            if (teleport.Target == null)
                return [];

            var changes = new HashSet<Teleport>();
            if (_points.TryGetValue(teleport.Target, out var target) && target.Target != null)
            {
                if (target.Target != teleport.Pos)
                {
                    if (_points.TryGetValue(target.Target, out var targetOfTarget) && targetOfTarget.Target != null)
                    {
                        targetOfTarget.Target = null;
                        changes.Add(targetOfTarget);
                    }
                }
                target.Target = null;
                changes.Add(target);
            }

            teleport.Target = null;
            changes.Add(teleport);
            return changes;
        }

        public IEnumerator<Teleport> GetEnumerator()
        {
            List<Teleport> list;
            lock (_pointsLock)
            {
                list = _points.Values.ToList();
            }
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
