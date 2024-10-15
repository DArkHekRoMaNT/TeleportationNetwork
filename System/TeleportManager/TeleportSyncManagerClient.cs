using TeleportationNetwork.Packets;
using Vintagestory.API.Client;
namespace TeleportationNetwork
{
    public class TeleportSyncManagerClient
    {
        private readonly IClientNetworkChannel _channel;
        private readonly TeleportManager _manager;

        public TeleportSyncManagerClient(ICoreClientAPI api, TeleportManager manager)
        {
            _manager = manager;
            _channel = api.Network.RegisterChannel(Constants.TeleportSyncChannelName)
                 .RegisterMessageType<SyncTeleportMessage>()
                 .RegisterMessageType<RemoveTeleportMessage>()
                 .RegisterMessageType<SyncTeleportListMessage>()
                 .SetMessageHandler<SyncTeleportMessage>(OnSyncTeleport)
                 .SetMessageHandler<RemoveTeleportMessage>(OnRemoveTeleport)
                 .SetMessageHandler<SyncTeleportListMessage>(OnSyncTeleportList);
        }

        public void UpdateTeleport(Teleport teleport)
        {
            _channel.SendPacket(new SyncTeleportMessage(teleport));
        }

        private void OnSyncTeleport(SyncTeleportMessage packet)
        {
            _manager.Points.AddOrUpdate(packet.Teleport);
        }

        private void OnRemoveTeleport(RemoveTeleportMessage packet)
        {
            _manager.Points.Remove(packet.Pos);
        }

        private void OnSyncTeleportList(SyncTeleportListMessage packet)
        {
            _manager.Points.SetFrom(packet.Points);
        }
    }
}
