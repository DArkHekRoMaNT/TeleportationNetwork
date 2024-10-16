using System.Linq;
using TeleportationNetwork.Packets;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class TeleportSyncManagerServer
    {
        private readonly ICoreServerAPI _api;
        private readonly IServerNetworkChannel _channel;
        private readonly TeleportManager _manager;

        public TeleportSyncManagerServer(ICoreServerAPI api, TeleportManager manager)
        {
            _api = api;
            _manager = manager;
            _channel = api.Network.RegisterChannel(Constants.TeleportSyncChannelName)
                .RegisterMessageType<SyncTeleportMessage>()
                .RegisterMessageType<RemoveTeleportMessage>()
                .RegisterMessageType<SyncTeleportListMessage>()
                .SetMessageHandler<SyncTeleportMessage>(OnUpdateTeleportFromClient);

            _manager.Points.ValueChanged += teleport =>
            {
                foreach (var player in _api.World.AllOnlinePlayers)
                {
                    var message = new SyncTeleportMessage(teleport.ForPlayerOnly(player.PlayerUID));
                    _channel.SendPacket(message, (IServerPlayer)player);
                }
            };

            _manager.Points.ValueRemoved += pos =>
            {
                _channel.BroadcastPacket(new RemoveTeleportMessage(pos));
                foreach (var point in _manager.Points.Where(x => x.Target == pos))
                {
                    _manager.Points.Unlink(point.Pos);
                }
            };
        }

        public void SendTeleportList(IServerPlayer player)
        {
            var points = _manager.Points.Select(x => x.ForPlayerOnly(player.PlayerUID)).ToArray();
            if (points.Length > 0)
            {
                _channel.SendPacket(new SyncTeleportListMessage(points), player);
            }
        }

        private void OnUpdateTeleportFromClient(IServerPlayer fromPlayer, SyncTeleportMessage msg)
        {
            if (!_manager.Points.TryGetValue(msg.Teleport.Pos, out var teleport))
            {
                return;
            }

            teleport.SetClientData(fromPlayer.PlayerUID, msg.Teleport.GetClientData(fromPlayer.PlayerUID));

            if (teleport.Name != msg.Teleport.Name)
            {
                int chunkSize = _api.World.BlockAccessor.ChunkSize;
                int chunkX = teleport.Pos.X / chunkSize;
                int chunkZ = teleport.Pos.Z / chunkSize;

                _api.WorldManager.LoadChunkColumnPriority(chunkX, chunkZ, new ChunkLoadOptions
                {
                    OnLoaded = () =>
                    {
                        if (fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative ||
                            _api.World.Claims.TryAccess(fromPlayer, teleport.Pos, EnumBlockAccessFlags.Use))
                        {
                            teleport.Name = msg.Teleport.Name;
                            _manager.Points.MarkDirty(teleport.Pos);
                        }
                    }
                });
            }

            _manager.Points.MarkDirty(teleport.Pos);
        }
    }
}
