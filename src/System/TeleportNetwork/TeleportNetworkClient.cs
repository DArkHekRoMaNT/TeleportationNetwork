using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportNetworkClient : TeleportNetwork, ITeleportNetworkClient
    {
        ICoreClientAPI ClientApi => (ICoreClientAPI)Api;
        IClientNetworkChannel ClientChannel => (IClientNetworkChannel)Channel;

        public override void Init(ICoreAPI api, ITeleportManager manager)
        {
            base.Init(api, manager);

            ClientChannel
                .RegisterMessageType(typeof(TeleportingData))
                .RegisterMessageType(typeof(SyncTeleportMessage))
                .SetMessageHandler<SyncTeleportMessage>(OnReceiveSyncPacket);
        }

        private void OnReceiveSyncPacket(SyncTeleportMessage message)
        {
            if (message.DoRemove)
            {
                Manager.RemoveTeleport(message.Teleport);
            }
            else
            {
                Manager.SetTeleport(message.Teleport);
            }
        }

        public void TeleportTo(Vec3d targetPos, Vec3d? sourcePos = null)
        {
            ClientApi.World.Player.Entity.SetActivityRunning(
                Core.ModId + "_teleportCooldown",
                Config.Current.TeleportCooldown);

            ClientChannel.SendPacket(new TeleportingData()
            {
                SourcePos = sourcePos,
                TargetPos = targetPos
            });
        }
    }

}