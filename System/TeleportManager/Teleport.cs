using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    [ProtoContract]
    public class Teleport
    {
        [ProtoMember(1)] public BlockPos Pos { get; private set; }

        [ProtoMember(2)]
        public string Name
        {
            get => _name;
            set => _name = string.IsNullOrEmpty(value) ? "Unknown" : value.Replace("{", "").Replace("}", "");
        }

        [ProtoMember(3)] public bool Enabled { get; set; }
        [ProtoMember(4)] public BlockPos? Target { get; set; }
        [ProtoMember(5)] public float Size { get; set; }
        [ProtoMember(6)] public int OrientationIndex { get; set; }
        [ProtoMember(7)] public List<string> ActivatedByPlayers { get; private set; }
        [ProtoMember(8)] public Dictionary<string, TeleportClientData> ClientData { get; private set; }

        public long LastUpdateTime { get; set; }

        public BlockFacing? Orientation => OrientationIndex == -1 ? null : BlockFacing.ALLFACES[OrientationIndex];

        private string _name = string.Empty;

        private Teleport()
        {
            Name = "Not setted";
            Enabled = false;
            Pos = new BlockPos(0);
            Target = null;
            Size = 0;
            OrientationIndex = -1;
            ActivatedByPlayers = [];
            ClientData = [];
            LastUpdateTime = 0;
        }

        public Teleport(BlockPos pos, string name, bool enabled, Block block) : this()
        {
            Pos = pos;
            Name = name;
            Enabled = enabled;
            UpdateBlockInfo(block);
        }

        public TeleportClientData GetClientData(ICoreClientAPI capi) => GetClientData(capi.World.Player.PlayerUID);

        public TeleportClientData GetClientData(string playerUID)
        {
            ClientData.TryGetValue(playerUID, out TeleportClientData? data);
            return data?.Clone() ?? new TeleportClientData();
        }

        public void SetClientData(ICoreClientAPI capi, TeleportClientData data) => SetClientData(capi.World.Player.PlayerUID, data);

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

        public Teleport ForPlayerOnly(string playerUID)
        {
            var teleport = (Teleport)MemberwiseClone();
            teleport.ClientData = new Dictionary<string, TeleportClientData>
            {
                { playerUID, GetClientData(playerUID) }
            };
            return teleport;
        }

        public void UpdateBlockInfo(Block block)
        {
            OrientationIndex = BlockFacing.FromCode(block.LastCodePart())?.Index ?? -1;
            Size = block.Attributes["gateSize"].AsFloat(0f);
        }
    }
}
