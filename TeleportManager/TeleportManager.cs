using TeleportationNetwork.Packets;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class TeleportManager : ModSystem
    {
        private ICoreAPI _api = null!;

        public TeleportListNew Points { get; } = [];
        public TeleportNameGenerator NameGenerator { get; } = new();

        private TeleportSyncManagerClient? _clientSyncManager;
        private TeleportSyncManagerServer? _serverSyncManager;

        public override void StartPre(ICoreAPI api)
        {
            _api = api;

            if (api is ICoreClientAPI capi)
            {
                _clientSyncManager = new TeleportSyncManagerClient(capi, this);
                capi.Network.RegisterChannel(Constants.TeleportManagerChannelName)
                    .RegisterMessageType<TeleportPlayerMessage>();
            }
            else if (api is ICoreServerAPI sapi)
            {
                _serverSyncManager = new TeleportSyncManagerServer(sapi, this);
                sapi.Network.RegisterChannel(Constants.TeleportManagerChannelName)
                    .RegisterMessageType<TeleportPlayerMessage>()
                    .SetMessageHandler<TeleportPlayerMessage>(OnTeleportPlayer);

                sapi.Event.PlayerJoin += _serverSyncManager.SendTeleportList;
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            var map = api.ModLoader.GetModSystem<WorldMapManager>();
            map.RegisterMapLayer<TeleportMapLayer>(Mod.Info.ModID, 1.0);
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            NameGenerator.Init(api);
        }

        public void TeleportPlayerTo(BlockPos pos)
        {
            if (_api is ICoreClientAPI capi)
            {
                capi.Network
                    .GetChannel(Constants.TeleportManagerChannelName)
                    .SendPacket(new TeleportPlayerMessage(pos));
            }
        }

        private void OnTeleportPlayer(IServerPlayer fromPlayer, TeleportPlayerMessage packet)
        {
            var pos = packet.Pos.ToVec3d().AddCopy(0.5, 1.5, 0.5);
            if (Points.TryGetValue(packet.Pos, out var teleport))
            {
                pos = teleport.GetTargetPos();
            }
            fromPlayer.Entity?.StabilityRelatedTeleportTo(pos, Mod.Logger);
        }

        public void CheckAllTeleportExists()
        {
            if (_api is ICoreServerAPI sapi)
            {
                foreach (var teleport in Points)
                {
                    int chunkSize = sapi.WorldManager.ChunkSize;
                    int chunkX = teleport.Pos.X / chunkSize;
                    int chunkZ = teleport.Pos.Z / chunkSize;

                    sapi.WorldManager.LoadChunkColumnPriority(chunkX, chunkZ, new ChunkLoadOptions
                    {
                        OnLoaded = () =>
                        {
                            var block = sapi.World.BlockAccessor.GetBlock(teleport.Pos);
                            var be = sapi.World.BlockAccessor.GetBlockEntity(teleport.Pos);
                            if (block is not BlockTeleport || be is not BlockEntityTeleport)
                            {
                                Points.Remove(teleport.Pos);
                                Mod.Logger.Notification($"Removed unknown teleport {teleport.Name} at {teleport.Pos}");
                            }
                        }
                    });
                }
            }
        }

        public void UpdateTeleport(Teleport teleport)
        {
            _clientSyncManager?.UpdateTeleport(teleport);
        }
    }
}
