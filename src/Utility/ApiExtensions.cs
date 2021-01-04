using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace TeleportationNetwork
{

    public static class ApiExtensions
    {
        public static string GetWorldId(this ICoreAPI api) => api?.World?.SavegameIdentifier;

        public static T LoadOrCreateConfig<T>(this ICoreAPI api, string path) where T : new()
        {
            // Try load config for this world
            try
            {
                T loadedWorldConfig = api.LoadModConfig<T>(GetWorldId(api) + "/" + path);
                if (loadedWorldConfig != null)
                {
                    api.StoreModConfig<T>(loadedWorldConfig, GetWorldId(api) + "/" + path);
                    return loadedWorldConfig;
                }
            }
            catch (Exception e)
            {
                api.World.Logger.ModError($"Failed loading file ({path}), error {e}. Will initialize new one");
            }

            // Else try load root config (and save as world config)
            try
            {
                T loadedRootConfig = api.LoadModConfig<T>(path);
                if (loadedRootConfig != null)
                {
                    api.StoreModConfig<T>(loadedRootConfig, path);
                    api.StoreModConfig<T>(loadedRootConfig, GetWorldId(api) + "/" + path);
                    return loadedRootConfig;
                }
            }
            catch (Exception e)
            {
                api.World.Logger.ModError($"Failed loading file ({path}), error {e}. Will initialize new one");
            }

            // Else create default config (and save as root and world config)
            var newConfig = new T();
            api.StoreModConfig<T>(newConfig, path);
            api.StoreModConfig<T>(newConfig, GetWorldId(api) + "/" + path);
            return newConfig;
        }

        public static TData LoadOrCreateDataFile<TData>(this ICoreAPI api, string filename) where TData : new()
        {
            var path = Path.Combine(GamePaths.DataPath, "ModData", GetWorldId(api), filename);
            try
            {
                if (File.Exists(path))
                {
                    var content = File.ReadAllText(path);
                    return JsonUtil.FromString<TData>(content);
                }
            }
            catch (Exception e)
            {
                api.World.Logger.ModError($"Failed loading file ({path}), error {e}. Will initialize new one");
            }
            var newData = new TData();
            SaveDataFile(api, filename, newData);
            return newData;
        }

        public static void SaveDataFile<TData>(this ICoreAPI api, string filename, TData data) where TData : new()
        {
            var path = Path.Combine(GamePaths.DataPath, "ModData", GetWorldId(api), filename);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var content = JsonUtil.ToString(data);
                File.WriteAllText(path, content);
            }
            catch (Exception e)
            {
                api.World.Logger.ModError($"Failed loading file ({path}), error {e}. Will initialize new one");
            }
        }
    }
}