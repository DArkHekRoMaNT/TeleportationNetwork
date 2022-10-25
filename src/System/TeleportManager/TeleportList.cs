using MonoMod.Utils;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    [ProtoContract]
    public class TeleportList
    {
        [ProtoMember(1)]
        private Dictionary<BlockPos, Teleport> _teleports = new();

        public event Action<Teleport>? OnValueChanged;
        public event Action<BlockPos>? OnValueRemoved;

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

                    OnValueChanged?.Invoke(value);
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
            _teleports[teleport.Pos] = teleport;
        }

        public bool Remove(BlockPos pos)
        {
            if (_teleports.Remove(pos))
            {
                OnValueRemoved?.Invoke(pos);
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
                OnValueChanged?.Invoke(value);
            }
        }

        public void SetFrom(TeleportList points)
        {
            _teleports.Clear();
            _teleports.AddRange(points._teleports);
        }

        public void SetFrom(IEnumerable<Teleport> points)
        {
            _teleports.Clear();
            foreach(var teleport in points)
            {
                _teleports.Add(teleport.Pos, teleport);
            }
        }
    }
}
