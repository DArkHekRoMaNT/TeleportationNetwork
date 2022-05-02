using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class Teleport : ITeleport
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public BlockPos Pos { get; set; }
        public List<string> ActivatedByPlayers { get; set; }
        public List<BlockPos> Neighbours { get; set; }

        public Teleport()
        {
            Enabled = false;
            Name = "null";
            Pos = new BlockPos();
            ActivatedByPlayers = new List<string>();
            Neighbours = new List<BlockPos>();
        }
    }
}