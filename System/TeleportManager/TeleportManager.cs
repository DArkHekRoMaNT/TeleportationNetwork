using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class TeleportManager : ModSystem
    {
        private IServerNetworkChannel? _serverChannel;
        private IClientNetworkChannel? _clientChannel;
        private ICoreAPI _api = null!;

        public TeleportList Points { get; } = new();
        public TeleportNameGenerator NameGenerator { get; } = new();

        public override void StartPre(ICoreAPI api)
        {
            _api = api;

            if (api is ICoreClientAPI capi)
            {
                _clientChannel = capi.Network
                     .RegisterChannel($"{Mod.Info.ModID}-teleport-manager")
                     .RegisterMessageType<SyncTeleportMessage>()
                     .RegisterMessageType<RemoveTeleportMessage>()
                     .RegisterMessageType<SyncTeleportListMessage>()
                     .SetMessageHandler<SyncTeleportMessage>(msg => Points.Set(msg.Teleport))
                     .SetMessageHandler<RemoveTeleportMessage>(msg => Points.Remove(msg.Pos))
                     .SetMessageHandler<SyncTeleportListMessage>(msg => Points.SetFrom(msg.Points))
                     .RegisterMessageType<TeleportPlayerMessage>();
            }
            else if (api is ICoreServerAPI sapi)
            {
                _serverChannel = sapi.Network
                    .RegisterChannel($"{Mod.Info.ModID}-teleport-manager")
                    .RegisterMessageType<SyncTeleportMessage>()
                    .RegisterMessageType<RemoveTeleportMessage>()
                    .RegisterMessageType<SyncTeleportListMessage>()
                    .RegisterMessageType<TeleportPlayerMessage>()
                    .SetMessageHandler<SyncTeleportMessage>(OnReceiveClientSyncTeleportMessage)
                    .SetMessageHandler<TeleportPlayerMessage>(OnReceiveTeleportPlayerMessage);

                Points.ValueChanged += teleport =>
                {
                    foreach (var player in _api.World.AllOnlinePlayers)
                    {
                        var message = new SyncTeleportMessage(teleport.ForPlayer(player.PlayerUID));
                        _serverChannel.SendPacket(message, (IServerPlayer)player);
                    }
                };

                Points.ValueRemoved += pos =>
                {
                    _serverChannel.BroadcastPacket(new RemoveTeleportMessage(pos));
                };

                sapi.Event.PlayerJoin += player =>
                {
                    _serverChannel.SendPacket(new SyncTeleportListMessage(Points.ForPlayer(player)), player);
                };
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            var mapManager = api.ModLoader.GetModSystem<WorldMapManager>();
            mapManager.RegisterMapLayer<TeleportMapLayer>(Mod.Info.ModID, 1.0);
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            NameGenerator.Init(api);
        }

        public void TeleportPlayerTo(BlockPos pos)
        {
            _clientChannel?.SendPacket(new TeleportPlayerMessage(pos));
        }

        private void OnReceiveTeleportPlayerMessage(IServerPlayer fromPlayer, TeleportPlayerMessage msg)
        {
            Vec3d pos = msg.Pos.ToVec3d().AddCopy(0.5, 1.5, 0.5);
            fromPlayer.Entity?.StabilityRelatedTeleportTo(pos, Mod.Logger);
        }

        public void CheckAllTeleportExists()
        {
            if (_api is ICoreServerAPI sapi)
            {
                foreach (Teleport teleport in Points.GetAll())
                {
                    teleport.Name = teleport.Name; // 1.12.0 -> 1.12.1 legacy name fix

                    int chunkSize = sapi.WorldManager.ChunkSize;
                    int chunkX = teleport.Pos.X / chunkSize;
                    int chunkZ = teleport.Pos.Z / chunkSize;

                    sapi.WorldManager.LoadChunkColumnPriority(chunkX, chunkZ, new ChunkLoadOptions
                    {
                        OnLoaded = delegate
                        {
                            BlockEntity be = sapi.World.BlockAccessor.GetBlockEntity(teleport.Pos);
                            if (be is not BlockEntityTeleport)
                            {
                                Points.Remove(teleport.Pos);
                                Mod.Logger.Notification("Removed unknown teleport {0} at {1}",
                                    teleport.Name, teleport.Pos);
                            }
                        }
                    });
                }
            }
        }

        public void UpdateTeleport(Teleport teleport)
        {
            _clientChannel?.SendPacket(new SyncTeleportMessage(teleport));
        }

        private void OnReceiveClientSyncTeleportMessage(IServerPlayer fromPlayer,
            SyncTeleportMessage msg)
        {
            var teleport = Points[msg.Teleport.Pos];
            if (teleport == null)
            {
                return;
            }

            teleport.SetClientData(fromPlayer.PlayerUID, msg.Teleport.GetClientData(fromPlayer.PlayerUID));

            if (teleport.Name != msg.Teleport.Name)
            {
                int chunkSize = _api.World.BlockAccessor.ChunkSize;
                int chunkX = teleport.Pos.X / chunkSize;
                int chunkZ = teleport.Pos.Z / chunkSize;

                ((ICoreServerAPI)_api).WorldManager.LoadChunkColumnPriority(chunkX, chunkZ, new ChunkLoadOptions
                {
                    OnLoaded = delegate
                    {
                        if (fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative ||
                            _api.World.Claims.TryAccess(fromPlayer, teleport.Pos, EnumBlockAccessFlags.Use))
                        {
                            teleport.Name = msg.Teleport.Name;
                            Points.MarkDirty(teleport.Pos);
                        }
                    }
                });
            }

            Points.MarkDirty(teleport.Pos);
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class RemoveTeleportMessage
        {
            public BlockPos Pos { get; set; }

            protected RemoveTeleportMessage() => Pos = null!;

            public RemoveTeleportMessage(BlockPos pos) => Pos = pos;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class SyncTeleportListMessage
        {
            public TeleportList Points { get; set; }

            protected SyncTeleportListMessage() => Points = null!;

            public SyncTeleportListMessage(TeleportList points) => Points = points;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class SyncTeleportMessage
        {
            public Teleport Teleport { get; set; }

            protected SyncTeleportMessage() => Teleport = null!;

            public SyncTeleportMessage(Teleport teleport) => Teleport = teleport;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class TeleportPlayerMessage
        {
            public BlockPos Pos { get; private set; }

            private TeleportPlayerMessage() => Pos = null!;

            public TeleportPlayerMessage(BlockPos pos) => Pos = pos;
        }
    }
}
