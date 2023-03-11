using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.WorldEdit;

namespace TeleportationNetwork
{
    public class ImportSchematicCommand : ServerChatCommand
    {
        public ImportSchematicCommand()
        {
            Command = "tpimp";
            Description = "[" + Constants.ModId + "] Import teleport schematic";
            Syntax = "/tpimp [list] or  /tpimp [paste|import|pasteraw] name";
            RequiredPrivilege = Privilege.gamemode;
            handler = Handler;
        }

        private void Handler(IServerPlayer player, int groupId, CmdArgs args)
        {
            switch (args?.PopWord())
            {
                case "help":
                    player.SendMessage(groupId, Syntax, EnumChatType.CommandSuccess);
                    break;


                case "list":
                    List<IAsset> schematics = player.Entity.Api.Assets
                        .GetMany(Constants.TeleportSchematicPath);

                    if (schematics == null || schematics.Count == 0)
                    {
                        player.SendMessage(groupId, Lang.Get(Constants.ModId + ":tpimp-empty"),
                            EnumChatType.CommandError);
                        break;
                    }

                    var list = new StringBuilder();
                    foreach (var sch in schematics)
                    {
                        list.AppendLine(sch.Name.Remove(sch.Name.Length - 5));
                    }
                    player.SendMessage(groupId, list.ToString(), EnumChatType.CommandSuccess);

                    break;


                case "paste":
                    BlockPos pos = player.Entity.Pos.AsBlockPos.Add(0, -1, 0);
                    LoadSchematic(player, groupId, args, "/tpimp paste [name]",
                        (schema) => PasteSchematic(schema, player.Entity.World, pos));
                    break;

                case "import":
                    LoadSchematic(player, groupId, args, "/tpimp import [name]",
                        (schema) => ImportSchematic(schema, player));
                    break;

                case "pasteraw":
                    BlockPos pos2 = player.Entity.Pos.AsBlockPos.Add(0, 3, 0);
                    LoadSchematic(player, groupId, args, "/tpimp pasteraw [name]",
                        (schema) => PasteSchematic(schema, player.Entity.World, pos2, false));
                    break;

                default:
                    player.SendMessage(groupId, Syntax, EnumChatType.CommandError);
                    break;
            }
        }

        private static void LoadSchematic(IServerPlayer player, int groupId, CmdArgs args, string help,
            Action<BlockSchematic> action)
        {
            string name = args.PopWord();
            if (name == null || name.Length == 0)
            {
                player.SendMessage(groupId, help, EnumChatType.CommandError);
                return;
            }

            string schemapath = $"{Constants.TeleportSchematicPath}/{name}.json";
            IAsset schema = player.Entity.Api.Assets.TryGet(schemapath);

            if (schema == null)
            {
                player.SendMessage(groupId, Lang.Get(Constants.ModId + ":tpimp-empty"),
                    EnumChatType.CommandError);
                return;
            }

            string? error = null;
            string fullpath = Path.Combine(schema.Origin.OriginPath,
                                           schema.Location.Domain,
                                           schema.Location.Path);
            BlockSchematic schematic = BlockSchematic.LoadFromFile(fullpath, ref error);

            if (error != null)
            {
                player.SendMessage(groupId, error, EnumChatType.CommandError);
                return;
            }

            action.Invoke(schematic);
        }

        private static void PasteSchematic(BlockSchematic schematic, IWorldAccessor world,
            BlockPos startPos, bool replaceMetaBlocks = true, EnumOrigin origin = EnumOrigin.BottomCenter)
        {
            BlockPos originPos = schematic.GetStartPos(startPos, origin);
            if (replaceMetaBlocks)
            {
                schematic.Place(world.BlockAccessor, world, originPos, replaceMetaBlocks);
            }
            else
            {
                schematic.Place(world.BulkBlockAccessor, world, originPos, replaceMetaBlocks);
                world.BulkBlockAccessor.Commit();
                PlaceEntitiesAndBlockEntitiesRaw(schematic, world.BlockAccessor, world, originPos);
            }
        }

        private static void PlaceEntitiesAndBlockEntitiesRaw(BlockSchematic schematic,
            IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos)
        {
            var blockPos = new BlockPos();
            foreach (KeyValuePair<uint, string> blockEntity2 in schematic.BlockEntities)
            {
                uint key = blockEntity2.Key;
                int num = (int)(key & 0x1FF);
                int num2 = (int)((key >> 20) & 0x1FF);
                int num3 = (int)((key >> 10) & 0x1FF);
                blockPos.Set(num + startPos.X, num2 + startPos.Y, num3 + startPos.Z);
                BlockEntity blockEntity = blockAccessor.GetBlockEntity(blockPos);
                if (blockEntity == null && blockAccessor is IWorldGenBlockAccessor)
                {
                    Block block = blockAccessor.GetBlock(blockPos, 1);
                    if (block.EntityClass != null)
                    {
                        blockAccessor.SpawnBlockEntity(block.EntityClass, blockPos);
                        blockEntity = blockAccessor.GetBlockEntity(blockPos);
                    }
                }

                if (blockEntity != null)
                {
                    Block block2 = blockAccessor.GetBlock(blockPos, 1);
                    var beClass = worldForCollectibleResolve.ClassRegistry
                        .GetBlockEntityClass(blockEntity.GetType());
                    if (block2.EntityClass != beClass)
                    {
                        worldForCollectibleResolve.Logger.Warning("Could not import block" +
                            " entity data for schematic at {0}. There is already {1}, expected {2}." +
                            " Probably overlapping ruins.", blockPos, blockEntity.GetType(),
                            block2.EntityClass);
                    }
                    else
                    {
                        ITreeAttribute treeAttribute = schematic.DecodeBlockEntityData(blockEntity2.Value);
                        treeAttribute.SetInt("posx", blockPos.X);
                        treeAttribute.SetInt("posy", blockPos.Y);
                        treeAttribute.SetInt("posz", blockPos.Z);
                        blockEntity.FromTreeAttributes(treeAttribute, worldForCollectibleResolve);
                        blockEntity.Pos = blockPos.Copy();
                    }
                }
            }

            Entity entity;
            foreach (string entity2 in schematic.Entities)
            {
                using var input = new MemoryStream(Ascii85.Decode(entity2));
                using var binaryReader = new BinaryReader(input);
                string entityClass = binaryReader.ReadString();
                entity = worldForCollectibleResolve.ClassRegistry.CreateEntity(entityClass);
                entity.FromBytes(binaryReader, isSync: false);
                entity.DidImportOrExport(startPos);
                if (blockAccessor is IWorldGenBlockAccessor worldGenBlockAccessor)
                {
                    worldGenBlockAccessor.AddEntity(entity);
                }
                else
                {
                    worldForCollectibleResolve.SpawnEntity(entity);
                }
            }
        }

        private static void ImportSchematic(BlockSchematic schematic, IServerPlayer player)
        {
            var we = player.Entity.Api.ModLoader.GetModSystem<WorldEdit>();
            if (we.CanUseWorldEdit(player, true))
            {
                FieldInfo toolFieldInfo = typeof(WorldEditWorkspace)
                    .GetField("ToolInstance", BindingFlags.NonPublic | BindingFlags.Instance);

                FieldInfo clipboardFieldInfo = typeof(WorldEditWorkspace)
                    .GetField("clipboardBlockData", BindingFlags.NonPublic | BindingFlags.Instance);

                var workspace = we.GetWorkSpace(player.PlayerUID);
                workspace.ToolsEnabled = true;
                workspace.SetTool("Import", player.Entity.Api);

                clipboardFieldInfo.SetValue(workspace, schematic);
                var tool = (ImportTool)toolFieldInfo.GetValue(workspace);

                tool.OnWorldEditCommand(we, new CmdArgs("imc"));

                we.SendPlayerWorkSpace(player.PlayerUID);
            }
        }
    }
}
