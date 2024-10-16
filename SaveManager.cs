using ProtoBuf;
using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace TeleportationNetwork
{
    public class SaveManager : ModSystem
    {
        private ICoreServerAPI _sapi = null!;
        private TeleportManager _teleportManager = null!;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartPre(ICoreAPI api)
        {
            if (api is ICoreServerAPI sapi)
            {
                _sapi = sapi;
                _teleportManager = sapi.ModLoader.GetModSystem<TeleportManager>();

                sapi.Event.GameWorldSave += OnSaveGame;
                sapi.Event.SaveGameLoaded += OnLoadGame;
            }
        }

        private void OnSaveGame()
        {
            var data = SerializerUtil.Serialize(_teleportManager.Points.ToArray());
            _sapi.WorldManager.SaveGame.StoreData("TPNetData", data);
        }

        private void OnLoadGame()
        {
            try
            {
                Mod.Logger.Event("Start loading data");

                byte[] data = _sapi.WorldManager.SaveGame.GetData("TPNetData");
                if (data != null)
                {
                    var array = DeserializeData(data);
                    _teleportManager.Points.SetFrom(array);
                    foreach (var teleport in array)
                    {
                        Mod.Logger.Debug($"Loaded teleport data for {teleport.Name} at {teleport.Pos}");
                    }
                    Mod.Logger.Event($"Data loaded for {array.Length} teleports");
                    Mod.Logger.Event("Check teleport exists (async)");
                    _sapi.ModLoader.GetModSystem<TeleportManager>().CheckAllTeleportExists();
                }
                else
                {
                    Mod.Logger.Event("No data for load");
                }
            }
            catch (Exception e)
            {
                Mod.Logger.Error("Failed loading data:\n{0}", e);
            }
        }

        private Teleport[] DeserializeData(byte[] data)
        {
            try
            {
                return SerializerUtil.Deserialize<Teleport[]>(data);
            }
            catch (ProtoException)
            {
                Mod.Logger.Debug("Failed loadind data. May be you mod version is too old or save corrupted");
                return [];
            }
        }
    }
}
