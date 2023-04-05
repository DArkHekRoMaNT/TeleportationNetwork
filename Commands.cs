using CommonLib.Extensions;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class Commands : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.ChatCommands
                .GetOrCreate("tpnet")
                .WithDescription("Teleportation Network commands")
                .RequiresPrivilege(Privilege.chat);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.ChatCommands
                .GetOrCreate("tpnet")
                .BeginSubCommand("dialog")
                    .WithDescription("Open teleport dialog")
                    .RequiresPrivilege(Privilege.tp)
                    .HandleWith(OpenTeleportDialog)
                .EndSubCommand();

            TextCommandResult OpenTeleportDialog(TextCommandCallingArgs args)
            {
                GuiDialogTeleportList dialog = new(api, null);
                dialog.TryOpen();
                return TextCommandResult.Success();
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            var parsers = api.ChatCommands.Parsers;
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
                            parsers.Word("name"),
                            parsers.WorldPosition("pos"),
                            parsers.OptionalBool("replace meta blocks", true))
                        .HandleWith(PasteTeleportSchematic)
                    .EndSubCommand()
                    .BeginSubCommand("import")
                        .WithDescription("Import teleport to WorldEdit")
                        .RequiresPlayer()
                        .WithArgs(parsers.Word("name"))
                        .HandleWith(ImportTeleportSchematic)
                    .EndSubCommand()
                .EndSubCommand();

            TextCommandResult ShowAllTeleportSchematic(TextCommandCallingArgs args)
            {
                List<IAsset> schematics = api.Assets.GetMany(Constants.TeleportSchematicPath);

                if (schematics == null || schematics.Count == 0)
                {
                    return TextCommandResult.Error(Lang.Get(Constants.ModId + ":tpimp-empty"));
                }

                var list = new StringBuilder();
                foreach (var sch in schematics)
                {
                    list.AppendLine(sch.Name.Remove(sch.Name.Length - 5));
                }
                return TextCommandResult.Success(list.ToString());
            }

            BlockSchematic? LoadTeleportSchematic(string name, ref string? error)
            {
                string schemapath = $"{Constants.TeleportSchematicPath}/{name}.json";
                IAsset schema = api.Assets.TryGet(schemapath);

                if (schema == null)
                {
                    error = Lang.Get(Constants.ModId + ":tpimp-empty");
                    return null;
                }

                string path;
                if (schema.Origin.OriginPath.EndsWith(schema.Location.Domain))
                {
                    path = Path.Combine(schema.Origin.OriginPath, schema.Location.Path);
                }
                else
                {
                    path = Path.Combine(schema.Origin.OriginPath, schema.Location.Domain, schema.Location.Path);
                }

                return BlockSchematic.LoadFromFile(path, ref error);
            }

            TextCommandResult PasteTeleportSchematic(TextCommandCallingArgs args)
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

                schematic.Paste(api.World, pos, replaceMetaBlocks);
                return TextCommandResult.Success();
            }

            TextCommandResult ImportTeleportSchematic(TextCommandCallingArgs args)
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
        }
    }
}
