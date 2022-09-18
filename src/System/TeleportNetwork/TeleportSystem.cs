using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class TeleportSystem : ModSystem
    {
        public ITeleportManager Manager { get; private set; }
        public ITeleportNetwork Network => (ITeleportNetwork)_serverNetwork ?? _clientNetwork;

        private ITeleportNetworkClient _clientNetwork;
        private ITeleportNetworkServer _serverNetwork;

        public override void AssetsLoaded(ICoreAPI api)
        {
            IAsset defaultNames = api.Assets.Get(new AssetLocation(Core.ModId, "config/names.json"));
            Manager = new TeleportManager(defaultNames?.ToObject<List<string>>());
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            _clientNetwork = new TeleportNetworkClient();
            _clientNetwork.Init(api, Manager);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            _serverNetwork = new TeleportNetworkServer();
            _serverNetwork.Init(api, Manager);
        }
    }
}