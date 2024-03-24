using CommonLib.Extensions;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class SchematicChatCommand : ServerChatCommandBase
    {
        public SchematicChatCommand(ICoreServerAPI api) : base(api)
        {
            api.ChatCommands
              .GetOrCreate("tpnet")
              .BeginSubCommand("schematic")
                  .RequiresPrivilege(Privilege.gamemode)
                  .WithDescription("Teleport schematic commands")
                  .BeginSubCommand("list")
                      .HandleWith(ShowAllTeleportSchematic)
                  .EndSubCommand()
                  .BeginSubCommand("paste")
                      .WithDescription("Place teleport on pos")
                      .WithArgs(
                          Parsers.Word("name"),
                          Parsers.WorldPosition("pos"),
                          Parsers.OptionalBool("replace meta blocks", true))
                      .HandleWith(PasteTeleportSchematic)
                  .EndSubCommand()
                  .BeginSubCommand("import")
                      .WithDescription("Import teleport to WorldEdit")
                      .RequiresPlayer()
                      .WithArgs(Parsers.Word("name"))
                      .HandleWith(ImportTeleportSchematic)
                  .EndSubCommand()
              .EndSubCommand();
        }

        private TextCommandResult ShowAllTeleportSchematic(TextCommandCallingArgs args)
        {
            List<IAsset> schematics = Api.Assets.GetMany(Constants.TeleportSchematicPath);

            if (schematics == null || schematics.Count == 0)
            {
                return TextCommandResult.Error(Lang.Get($"{Constants.ModId}:tpimp-empty"));
            }

            var sb = new StringBuilder();
            foreach (var schematic in schematics)
            {
                sb.AppendLine(schematic.Name.Remove(schematic.Name.Length - 5));
            }
            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult PasteTeleportSchematic(TextCommandCallingArgs args)
        {
            string name = (string)args[0];
            BlockPos pos = ((Vec3d)args[1]).AsBlockPos;
            bool replaceMetaBlocks = (bool)args[2];

            string? error = null;
            var schematic = LoadTeleportSchematic(name, ref error);

            if (error != null || schematic == null)
            {
                return TextCommandResult.Error(error);
            }

            schematic.Paste(Api.World, pos, replaceMetaBlocks);
            return TextCommandResult.Success();
        }

        private TextCommandResult ImportTeleportSchematic(TextCommandCallingArgs args)
        {
            string name = (string)args[0];

            string? error = null;
            var schematic = LoadTeleportSchematic(name, ref error);

            if (error != null || schematic == null)
            {
                return TextCommandResult.Error(error);
            }


            schematic.ImportToWorldEdit((IServerPlayer)args.Caller.Player);
            return TextCommandResult.Success();
        }

        private BlockSchematic? LoadTeleportSchematic(string name, ref string? error)
        {
            string schemaPath = $"{Constants.TeleportSchematicPath}/{name}.json";
            IAsset schema = Api.Assets.TryGet(schemaPath);

            if (schema == null)
            {
                error = Lang.Get($"{Constants.ModId}:tpimp-empty");
                return null;
            }

            string? path;
            string? dir = Path.GetDirectoryName(schema.Origin.OriginPath);
            if (dir != null && dir.EndsWith(schema.Location.Domain))
            {
                path = Path.Combine(schema.Origin.OriginPath, schema.Location.Path);
            }
            else
            {
                path = Path.Combine(schema.Origin.OriginPath, schema.Location.Domain, schema.Location.Path);
            }

            return BlockSchematic.LoadFromFile(path, ref error);
        }
    }
}
