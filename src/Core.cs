using SharedUtils.Extensions;
using SharedUtils.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;
using System.Collections.Generic;

namespace TeleportationNetwork
{
    public class Core : ModSystem
    {
        public Core()
        {
            Instance = this;
        }

        public static Core Instance { get; private set; }

        /// <summary>
        /// Common ModLogger. For blocks, items, ui and other.
        /// In other mod system use inner Mod.Logger instead
        /// </summary>
        public static ILogger ModLogger => Instance.Mod.Logger;
        public static string ModId => Instance.Mod.Info.ModID;
        public static string ModPrefix => $"[{ModId}] ";


        public HudCircleRenderer HudCircleRenderer { get; private set; }

        private ICoreAPI api;

        public override void StartPre(ICoreAPI api)
        {
            Config.Current = api.LoadOrCreateConfig<Config>(ModId + ".json");
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            ClassRegister();

            TreeAttribute.RegisterAttribute(Constants.AttributesId + 1, typeof(BlockPosArrayAttribute));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            HudCircleRenderer = new HudCircleRenderer(api, new HudCircleSettings() { Color = 0x23cca2 });
        }

        private void ClassRegister()
        {
            api.RegisterBlockClass("BlockBrokenTeleport", typeof(BlockBrokenTeleport));
            api.RegisterBlockClass("BlockNormalTeleport", typeof(BlockNormalTeleport));
            api.RegisterBlockEntityClass("BETeleport", typeof(BETeleport));
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            // Don't patch for default distance
            if (Config.Current.MinTeleportDistance != 4096)
            {
                UpdateMinTeleportDistance();
            }
        }

        private void UpdateMinTeleportDistance()
        {
            // Get the patched structures.json file
            IAsset asset = api.Assets.Get("worldgen/structures.json");
            var structuresConfig = asset.ToObject<WorldGenStructuresConfig>();
            var teleportStructures = new List<int>();

            // Loop through the patches structures, save the indices of the teleports
            for (var i = 0; i < structuresConfig.Structures.Length; i++)
            {
                WorldGenStructure wgstruct = structuresConfig.Structures[i];
                if (wgstruct.Code.StartsWith("tpnet_teleport")) { teleportStructures.Add(i); }
            }

            // Construct a patch for each of the teleport structures; the path is /structures/index/minGroupDistance
            var patches = new List<JsonPatch>();
            foreach (int teleportIndex in teleportStructures)
            {
                patches.Add(new JsonPatch
                {
                    Op = EnumJsonPatchOp.Replace,
                    File = new AssetLocation("game:worldgen/structures.json"),
                    Path = "/structures/" + teleportIndex + "/minGroupDistance",
                    Value = JsonObject.FromJson(Config.Current.MinTeleportDistance.ToString())
                });
            }
            api.ApplyJsonPatches(patches);
        }

        public override void Dispose()
        {
            base.Dispose();
            Instance = null;
        }
    }
}
