using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class ConfigManager : ModSystem
    {
        private IServerNetworkChannel? _serverChannel;
        private ICoreAPI _api = null!;

        public Dictionary<Type, object> Configs { get; } = new();

        public override double ExecuteOrder() => 0.001;

        public override void StartPre(ICoreAPI api)
        {
            _api = api;
            LoadAllConfigs();

            if (_api is ICoreServerAPI sapi)
            {
                _serverChannel = sapi.Network
                    .RegisterChannel(Mod.Info.ModID + "-config-manager")
                    .RegisterMessageType<SyncConfigPacket>();
            }

            if (_api is ICoreClientAPI capi)
            {
                capi.Network
                    .RegisterChannel(Mod.Info.ModID + "-config-manager")
                    .RegisterMessageType<SyncConfigPacket>()
                    .SetMessageHandler<SyncConfigPacket>(OnSyncConfigPacketReceived);
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.RegisterCommand(new ConfigServerCommand(api, typeof(Config)));
            api.Event.PlayerJoin += byPlayer =>
            {
                foreach(var config in Configs)
                {
                    MarkConfigDirty(config.Key);
                }
            };
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.RegisterCommand(new ConfigClientCommand(api, typeof(Config)));
        }

        private void LoadAllConfigs()
        {
            Configs.Clear();
            foreach (Type type in GetConfigTypes(Assembly.GetExecutingAssembly()))
            {
                var config = Activator.CreateInstance(type);
                config = ConfigUtil.LoadConfig(_api, type, config);
                ConfigUtil.SaveConfig(_api, type, config);
                Configs.Add(type, config);
            }
        }

        private static IEnumerable<Type> GetConfigTypes(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(ConfigAttribute), true).Length > 0)
                {
                    yield return type;
                }
            }
        }

        public void SaveAllConfigs(ICoreAPI api)
        {
            foreach (var config in Configs)
            {
                ConfigUtil.SaveConfig(api, config.Value);
            }
        }

        public void MarkConfigDirty(Type type)
        {
            if (Configs.TryGetValue(type, out object config))
            {
                config = ConfigUtil.CheckConfig(type, config);
                ConfigUtil.SaveConfig(_api, type, config);
                if (_serverChannel != null)
                {
                    byte[] data = ConfigUtil.Serialize(config);
                    _serverChannel.BroadcastPacket(new SyncConfigPacket(data, config.GetType()));
                }
            }
        }

        private void OnSyncConfigPacketReceived(SyncConfigPacket packet)
        {
            if (Configs.TryGetValue(packet.Type, out object config))
            {
                Configs[packet.Type] = ConfigUtil.Deserialize(config, packet.Data);
            }
        }

        public T GetConfig<T>()
        {
            return (T)GetConfig(typeof(T));
        }

        public object GetConfig(Type type)
        {
            return Configs[type];
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class SyncConfigPacket
        {
            public Type Type { get; private set; }
            public byte[] Data { get; private set; }
            private SyncConfigPacket() { Data = null!; Type = null!; }
            public SyncConfigPacket(byte[] data, Type type) { Data = data; Type = type; }
        }
    }
}
