using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public interface ITeleport
    {
        bool Enabled { get; }
        string Name { get; }
        BlockPos Pos { get; }
        List<string> ActivatedByPlayers { get; }
        List<BlockPos> Neighbours { get; }
    }
}