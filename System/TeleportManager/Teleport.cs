using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class Teleport
    {
        public BlockPos Pos { get; private set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public List<Teleport> Neighbours { get; private set; }
        public List<string> ActivatedByPlayers { get; private set; }
        public Dictionary<string, TeleportClientData> ClientData { get; private set; }

        private Teleport()
        {
            Name = "null";
            Enabled = false;
            Pos = new BlockPos();
            Neighbours = new List<Teleport>();
            ActivatedByPlayers = new List<string>();
            ClientData = new Dictionary<string, TeleportClientData>();
        }

        public Teleport(BlockPos pos) : this()
        {
            Pos = pos;
        }

        public Teleport(BlockPos pos, string name, bool enabled = false) : this()
        {
            Pos = pos;
            Name = name;
            Enabled = enabled;
        }

        public TeleportClientData GetClientData(string playerUID)
        {
            ClientData.TryGetValue(playerUID, out TeleportClientData data);
            return data?.Clone() ?? new TeleportClientData();
        }

        public TeleportClientData GetClientData(ICoreClientAPI capi)
            => GetClientData(capi.World.Player.PlayerUID);

        public void SetClientData(string playerUID, TeleportClientData data)
        {
            if (ClientData.ContainsKey(playerUID))
            {
                ClientData[playerUID] = data;
            }
            else
            {
                ClientData.Add(playerUID, data);
            }
        }

        public void SetClientData(ICoreClientAPI capi, TeleportClientData data)
            => SetClientData(capi.World.Player.PlayerUID, data);

        public Teleport ForPlayer(string playerUID)
        {
            var teleport = (Teleport)MemberwiseClone();
            teleport.ClientData = new Dictionary<string, TeleportClientData>()
            {
                { playerUID, GetClientData(playerUID) }
            };
            return teleport;
        }
    }
}
