using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class Teleport
    {
        public BlockPos Pos { get; private set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public List<Teleport> Neighbours { get; private set; }
        public List<string> ActivatedByPlayers { get; private set; }

        private Teleport()
        {
            Name = "null";
            Enabled = false;
            Pos = new BlockPos();
            Neighbours = new List<Teleport>();
            ActivatedByPlayers = new List<string>();
        }

        public Teleport(BlockPos pos) : this()
        {
            Pos = pos;
        }

        public Teleport(BlockPos pos, string name, bool enabled = false) : this()
        {
            Pos = pos;
            Name = name;
            Enabled = enabled;
        }
    }
}