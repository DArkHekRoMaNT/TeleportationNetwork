using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace TeleportationNetwork
{
    public class TeleportStructureData
    {
        private readonly ILogger _logger;
        private readonly ICoreServerAPI _api;

        private readonly TeleportSchematicStructure[][] _structures;
        private readonly TeleportSchematicStructure[][] _pillars;
        private readonly TeleportSchematicStructure[][] _bases;

        private int _orientation = 0;
        private int _structureId = 0;
        private int _pillarId = -1;
        private int _baseId = -1;

        public TeleportStructureProperties Props { get; }
        public AssetLocation[] ContainsBlockCodes { get; }

        public TeleportSchematicStructure Teleport => _structures[_structureId][_orientation];
        public TeleportSchematicStructure? Base => _baseId == -1 ? null : _bases[_baseId][_orientation];
        public TeleportSchematicStructure? Pillar => _pillarId == -1 ? null : _pillars[_pillarId][_orientation];

        public TeleportStructureData(TeleportStructureProperties props, ICoreServerAPI api, ILogger logger)
        {
            Props = props;
            _api = api;
            _logger = logger;

            var blockLayerConfig = api.Assets.Get("game:worldgen/blocklayers.json").ToObject<BlockLayerConfig>();
            //blockLayerConfig.ResolveBlockIds(api);

            _structures = LoadSchematicList(blockLayerConfig, props.Schematics);
            _bases = LoadSchematicList(blockLayerConfig, props.BaseSchematics);
            _pillars = LoadSchematicList(blockLayerConfig, props.PillarSchematics);

            var blockCodes = new List<AssetLocation>();
            blockCodes.AddRange(GetBlockCodes(_structures));
            blockCodes.AddRange(GetBlockCodes(_bases));
            blockCodes.AddRange(GetBlockCodes(_pillars));
            ContainsBlockCodes = blockCodes.Distinct().ToArray();
        }

        private TeleportSchematicStructure[][] LoadSchematicList(BlockLayerConfig config, string[] names)
        {
            var schematics = new List<TeleportSchematicStructure[]>();
            foreach (string name in names)
            {
                var schematic = LoadSchematic(config, name);
                if (schematic != null)
                {
                    schematics.Add(schematic);
                }
            }
            return schematics.ToArray();
        }

        private TeleportSchematicStructure[]? LoadSchematic(BlockLayerConfig config, string name)
        {
            IAsset[] assets;

            if (name.EndsWith("*"))
            {
                string subName = name[..^1];
                assets = _api.Assets.GetManyInCategory("worldgen", $"schematics/{subName}").ToArray();
            }
            else
            {
                assets = [_api.Assets.Get($"worldgen/schematics/{name}.json")];
            }

            foreach (IAsset asset in assets)
            {
                var schematic = asset.ToObject<TeleportSchematicStructure>();
                if (schematic == null)
                {
                    _logger.Warning("Could not load {0}", name);
                    continue;
                }

                schematic.FromFileName = asset.Name;

                var rotatedSchematics = new TeleportSchematicStructure[4];
                rotatedSchematics[0] = schematic;

                for (int k = 0; k < 4; k++)
                {
                    if (k > 0)
                    {
                        rotatedSchematics[k] = rotatedSchematics[0].Copy();
                        rotatedSchematics[k].TransformWhilePacked(_api.World, EnumOrigin.BottomCenter, k * 90);
                    }
                    rotatedSchematics[k].blockLayerConfig = config;
                    rotatedSchematics[k].Init(_api.World.BlockAccessor);
                    rotatedSchematics[k].LoadMetaInformationAndValidate(_api.World.BlockAccessor,
                        _api.World, schematic.FromFileName);
                }

                return rotatedSchematics;
            }

            return null;
        }

        private static AssetLocation[] GetBlockCodes(TeleportSchematicStructure[][] schematics)
        {
            var blockCodes = new List<AssetLocation>();
            foreach (TeleportSchematicStructure[] schematic in schematics)
            {
                foreach (TeleportSchematicStructure rotatedSchematic in schematic)
                {
                    blockCodes.AddRange(rotatedSchematic.BlockCodes.Values.ToArray());
                }
            }
            return blockCodes.ToArray();
        }

        public void Randomize(LCGRandom random)
        {
            _orientation = random.NextInt(4);
            _structureId = random.NextInt(_structures.Length);

            if (_bases.Length > 0)
            {
                _baseId = random.NextInt(_bases.Length);
            }

            if (_pillars.Length > 0)
            {
                _pillarId = random.NextInt(_pillars.Length);
            }
        }
    }
}
