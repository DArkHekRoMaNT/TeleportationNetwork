using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public interface ITeleportManager
    {
        void SetTeleport(ITeleport teleport);
        void RemoveTeleport(ITeleport teleport);
        ITeleport GetTeleport(BlockPos pos);
        void SetTeleports(IEnumerable<ITeleport> teleports);
        void AddTeleports(IEnumerable<ITeleport> teleports);

        Action<ITeleport> OnAdded { get; set; }
        Action<ITeleport> OnRemoved { get; set; }
        Action<ITeleport> OnModified { get; set; }
        Action<ITeleport, IPlayer> OnActivatedByPlayer { get; set; }

        List<ITeleport> GetAllTeleports(System.Func<ITeleport, bool> predicate = null);
        List<ITeleport> GetAllEnabledTeleports();
        List<ITeleport> GetAllEnabledActivatedByPlayer(IPlayer player);

        List<ITeleport> GetAllNeighbours(ITeleport teleport, System.Func<ITeleport, bool> predicate = null);
        List<ITeleport> GetAllEnabledNeighbours(ITeleport teleport);
        List<ITeleport> GetAllEnabledNeighboursActivatedByPlayer(ITeleport teleport, IPlayer player);

        void ActivateTeleport(ITeleport teleport, IPlayer player);

        ITeleport CreateTeleport(BlockPos pos, bool enabled);
        ITeleport GetOrCreateTeleport(BlockPos pos, bool enabled);
    }
}