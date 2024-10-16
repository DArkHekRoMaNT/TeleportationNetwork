using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace TeleportationNetwork.Generation.v2
{
    public class WorldGenSystem : ModSystem
    {
        public StructureRandomizer Randomizer { get; private set; } = null!;

        private ICoreServerAPI _api = null!;
        private IWorldGenBlockAccessor _worldgenBlockAccessor = null!;

        public override void StartServerSide(ICoreServerAPI api)
        {
            _api = api;
            _api.Event.WorldgenHook(Handler, "standard", $"{Constants.ModId}:genTeleportStructure");
            _api.Event.GetWorldgenBlockAccessor((chunkProvider) =>
            {
                _worldgenBlockAccessor = chunkProvider.GetBlockAccessor(true);
            });

            Randomizer = new StructureRandomizer(api);
        }

        private void Handler(IBlockAccessor blockAccessor, BlockPos pos, string param)
        {
            _worldgenBlockAccessor.SetBlock(1, pos);
        }
    }
}
