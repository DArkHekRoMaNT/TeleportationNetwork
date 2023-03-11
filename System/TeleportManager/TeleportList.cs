using MonoMod.Utils;
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
        private Dictionary<BlockPos, Teleport> _teleports = new();

        public event Action? Changed;
        public event Action<Teleport>? ValueChanged;
        public event Action<BlockPos>? ValueRemoved;

        public int Count => _teleports.Count;

        public Teleport? this[BlockPos pos]
        {
            get => _teleports.TryGetValue(pos, out Teleport value) ? value : null;
            set
            {
                if (value != null)
                {

                    if (_teleports.ContainsKey(pos))
                    {
                        _teleports[pos] = value;
                    }
                    else
                    {
                        _teleports.Add(value.Pos, value);
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
            var list = _teleports.Values.ToList();
            return predicate == null ? list : list.FindAll(predicate);
        }

        public void Set(Teleport teleport)
        {
            this[teleport.Pos] = teleport;
        }

        public bool Remove(BlockPos pos)
        {
            if (_teleports.Remove(pos))
            {
                ValueRemoved?.Invoke(pos);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public bool Contains(BlockPos pos)
        {
            return _teleports.ContainsKey(pos);
        }

        public void MarkDirty(BlockPos pos)
        {
            Teleport? value = this[pos];
            if (value != null)
            {
                ValueChanged?.Invoke(value);
            }
        }

        public void SetFrom(TeleportList points)
        {
            _teleports.Clear();
            _teleports.AddRange(points._teleports);
            Changed?.Invoke();
        }

        public void SetFrom(IEnumerable<Teleport> points)
        {
            _teleports.Clear();
            foreach (var teleport in points)
            {
                _teleports.Add(teleport.Pos, teleport);
            }
            Changed?.Invoke();
        }

        public TeleportList ForPlayer(IServerPlayer player)
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
