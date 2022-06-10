using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public interface ITeleportNetwork
    {
        ITeleportManager Manager { get; }
        INetworkChannel Channel { get; }
        ICoreAPI Api { get; }

        void Init(ICoreAPI api, ITeleportManager manager);
    }

    public interface ITeleportNetworkClient : ITeleportNetwork
    {
        void TeleportTo(Vec3d targetPos, Vec3d? sourcePos = null);
    }

    public interface ITeleportNetworkServer : ITeleportNetwork
    {
    }
}