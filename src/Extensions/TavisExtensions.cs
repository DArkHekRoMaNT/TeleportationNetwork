
// Credit to Apache from Vintage Story Discord

using Vintagestory.API.Common;
using Vintagestory.ServerMods.NoObf;
using System.Collections.Generic;

namespace TeleportationNetwork
{
    /// <summary>
    ///     Extension methods for Tavis JSON Patching Engine.
    /// </summary>
    /// <example>
    ///     var patch = new JsonPatch
    ///     {
    ///         Op = EnumJsonPatchOp.Replace,
    ///         File = new AssetLocation("game:entities/land/drifter.json"),
    ///         Path = "/server/spawnconditions/runtime/maxQuantityByType/*-normal",
    ///         Value = JsonObject.FromJson("5")
    ///     };
    ///     api.ApplyJsonPatch(patch);
    /// </example>
    public static class TavisExtensions
    {
        private static int _dummyValue = 0;

        /// <summary>
        ///     Applies a single patch to a JSON file.
        /// </summary>
        /// <param name="api">The core API used by the game, on both the client, and the server.</param>
        /// <param name="patch">The patch to apply.</param>
        public static void ApplyJsonPatch(this ICoreAPI api, JsonPatch patch)
        {
            // Still using these awkward pass by reference dummy values.
            // Ideally, the part of the method that actually adds the patch should be extracted.
            var jsonPatcher = api.ModLoader.GetModSystem<ModJsonPatchLoader>();
            jsonPatcher.ApplyPatch(0, patch.File, patch, ref _dummyValue, ref _dummyValue, ref _dummyValue);
        }

        /// <summary>
        ///     Applies a number of patches to the JSON assets of the game.
        /// </summary>
        /// <param name="api">The core API used by the game, on both the client, and the server.</param>
        /// <param name="patches">The patches to apply.</param>
        public static void ApplyJsonPatches(this ICoreAPI api, IEnumerable<JsonPatch> patches)
        {
            var jsonPatcher = api.ModLoader.GetModSystem<ModJsonPatchLoader>();
            foreach (var patch in patches)
            {
                jsonPatcher.ApplyPatch(0, patch.File, patch, ref _dummyValue, ref _dummyValue, ref _dummyValue);
            }
        }
    }
}
