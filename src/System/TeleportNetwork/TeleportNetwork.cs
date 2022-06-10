using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public abstract class TeleportNetwork : ITeleportNetwork
    {
        public ITeleportManager Manager { get; private set; }
        public ICoreAPI Api { get; private set; }
        public INetworkChannel Channel { get; private set; }

        public virtual void Init(ICoreAPI api, ITeleportManager manager)
        {
            Manager = manager;
            Api = api;

            Channel = Api.Network.RegisterChannel("tpnet");

            manager.OnAdded += OnTeleportAdded;
            manager.OnModified += OnTeleportModified;
            manager.OnRemoved += OnTeleportRemoved;
            manager.OnActivatedByPlayer += OnTeleportActivatedByPlayer;
        }

        protected virtual void OnTeleportAdded(ITeleport teleport)
        {
            string type = teleport.Enabled ? "normal" : "broken";
            Core.ModLogger.Debug($"Added teleport {teleport.Name} ({type}) at {teleport.Pos} to teleports list");
        }

        protected virtual void OnTeleportModified(ITeleport teleport)
        {
            string type = teleport.Enabled ? "normal" : "broken";
            Core.ModLogger.Debug($"Modified teleport {teleport.Name} ({type}) at {teleport.Pos}");
        }

        protected virtual void OnTeleportRemoved(ITeleport teleport)
        {
            string type = teleport.Enabled ? "normal" : "broken";
            Core.ModLogger.Debug($"Removed teleport {teleport.Name} ({type}) at {teleport.Pos} from teleports list");
        }

        protected virtual void OnTeleportActivatedByPlayer(ITeleport teleport, IPlayer player)
        {
            string type = teleport.Enabled ? "normal" : "broken";
            Core.ModLogger.Debug($"{player.PlayerName} activated teleport {teleport.Name} ({type}) at {teleport.Pos}");
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct SyncTeleportMessage
    {
        public Teleport Teleport { get; set; }
        public bool DoRemove { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct TeleportingData
    {
        public Vec3d? SourcePos { get; set; }
        public Vec3d TargetPos { get; set; }
    }
}