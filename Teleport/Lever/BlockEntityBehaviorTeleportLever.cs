using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

//TODO: Использовать F для настроек телепорта? Или как там в клаттерах. С настройкой размера и т.п.
//TODO: Remove linked-teleport-info
//TODO: Cделать отдельный шейп для инвентаря
//TODO: Обновить звук
//TODO: +Запретить прямое взаимодействие телепорта
//TODO: Обновить подсказку после починки
//TODO: Сломанные варианты рычага и телепорта
//TODO: Коллижн и ломание в сингле
//TODO: ~~Объединить телепорт и рычаг?
//TODO: Не хранить размер в телепорте, рассчитывать после телепорта
//TODO: ~~Entity для коллайда
//TODO: Десинк открытия/закрытия пофиксить, бесит
//TODO: Проверить лодки и оленя
//TODO: Сдвинуть телепорт к стене + нормальная синхронизация шейпа и шейдера
//TODO: Свет сломанный?

namespace TeleportationNetwork
{
    public class BlockEntityTeleportLever : BlockEntity
    {
        private TeleportManager _manager = null!;
        private BlockPos? _teleportPos;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _manager = Api.ModLoader.GetModSystem<TeleportManager>();
        }

        public Teleport? GetTeleport()
        {
            if (_teleportPos == null)
            {
                return null;
            }

            if (_manager.Points.TryGetValue(_teleportPos, out var teleport))
            {
                return teleport;
            }
            else
            {
                Unlink();
                return null;
            }
        }

        public bool TryLink(BlockPos pos)
        {
            Unlink();
            _teleportPos = pos;
            return true;
        }

        public void Unlink()
        {
            if (_teleportPos != null)
            {
                _teleportPos = null;
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            var teleport = GetTeleport();
            if (teleport != null && Api.World.BlockAccessor.GetBlockEntity(teleport.Pos) is BlockEntityTeleport be)
            {
                be.GetBlockInfo(forPlayer, dsc);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (_teleportPos != null)
            {
                tree.SetBlockPos($"teleportPos", _teleportPos);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _teleportPos = tree.GetBlockPos($"teleportPos");
        }
    }

    public class BlockTeleportLever : Block
    {
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (world.BlockAccessor.GetBlockEntity(selection.Position) is BlockEntityTeleportLever be)
            {
                var teleport = be.GetTeleport();
                if (teleport != null && world.BlockAccessor.GetBlock(teleport.Pos) is BlockTeleport tp)
                {
                    var teleportSel = selection.Clone();
                    teleportSel.Position = teleport.Pos;
                    return tp.GetPlacedBlockInteractionHelp(world, teleportSel, forPlayer);
                }
            }

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTeleportLever be)
            {
                var teleport = be.GetTeleport();
                if (teleport != null && world.BlockAccessor.GetBlock(teleport.Pos) is BlockTeleport tp)
                {
                    var teleportSel = blockSel.Clone();
                    teleportSel.Position = teleport.Pos;
                    return tp.OnBlockInteractStart(world, byPlayer, teleportSel);
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }

    public class ItemTeleportLeverLinker : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel?.Position == null)
                return;

            if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTeleport)
            {
                slot.Itemstack.Attributes.SetBlockPos("teleportPos", blockSel.Position);
                handling = EnumHandHandling.PreventDefault;
                return;
            }
            else
            {
                var teleportPos = slot.Itemstack.Attributes.GetBlockPos("teleportPos");
                if (teleportPos != null && api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTeleportLever be)
                {
                    if (be.TryLink(teleportPos))
                    {
                        slot.Itemstack.Attributes.RemoveAttribute("teleportPosX");
                        slot.Itemstack.Attributes.RemoveAttribute("teleportPosY");
                        slot.Itemstack.Attributes.RemoveAttribute("teleportPosZ");
                        handling = EnumHandHandling.PreventDefault;
                        return;
                    }
                }
            }
        }
    }

    public class ItemTeleportLeverLinkerOld : Item
    {
        private BlockPos? _teleportPos;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTeleport)
            {
                _teleportPos = blockSel.Position;
                handling = EnumHandHandling.PreventDefault;
                return;
            }
            else if (_teleportPos != null)
            {
                var be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (be != null)
                {
                    var beb = new BlockEntityBehaviorTeleportLever(be);
                    if (beb.TryLink(_teleportPos))
                    {
                        _teleportPos = null;
                        be.Behaviors.Add(beb);
                        handling = EnumHandHandling.PreventDefault;
                        return;
                    }
                }
            }
        }
    }

    public class BlockEntityBehaviorTeleportLever : BlockEntityBehavior
    {
        private readonly TeleportManager _manager;
        private BlockPos? _teleportPos;

        public BlockEntityBehaviorTeleportLever(BlockEntity blockentity) : base(blockentity)
        {
            _manager = Api.ModLoader.GetModSystem<TeleportManager>();

            if (Block.GetBehavior<BlockBehaviorTeleportLever>() == null)
            {
                Block.CollectibleBehaviors = Block.CollectibleBehaviors
                    .Append(new BlockBehaviorTeleportLever(Block));
            }
        }

        public Teleport? GetTeleport()
        {
            if (_teleportPos == null)
            {
                return null;
            }

            if (_manager.Points.TryGetValue(_teleportPos, out var teleport))
            {
                return teleport;
            }
            else
            {
                Unlink();
                return null;
            }
        }

        public bool TryLink(BlockPos pos)
        {
            Unlink();
            _teleportPos = pos;
            return true;
        }

        public void Unlink()
        {
            if (_teleportPos != null)
            {
                _teleportPos = null;
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            var teleport = GetTeleport();
            if (teleport != null)
            {
                dsc.AppendLine(Lang.Get($"{Constants.ModId}:linked-teleport-info", teleport.Name));
            }
        }

        public bool Interact(IWorldAccessor world, IPlayer byPlayer, ref EnumHandling handling)
        {
            if (GetTeleport()?.Enabled != true)
            {
                return false;
            }

            if (world.BlockAccessor.GetBlockEntity(_teleportPos) is BlockEntityTeleport be)
            {
                if (byPlayer.Entity.Controls.Sneak)
                {
                    be.OpenEditDialog();
                }
                else
                {
                    be.OpenTeleportDialog();
                }
                handling = EnumHandling.PreventDefault;
                return true;
            }

            return false;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (_teleportPos != null)
            {
                tree.SetBlockPos($"{Constants.ModId}_teleportPos", _teleportPos);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _teleportPos = tree.GetBlockPos($"{Constants.ModId}_teleportPos");
        }
    }

    public class BlockBehaviorTeleportLever(Block block) : BlockBehavior(block)
    {
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            var interactions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);

            var beb = GetBehavior(world, selection.Position);
            if (beb?.GetTeleport()?.Enabled ?? false)
            {
                interactions = interactions
                    .Append(new WorldInteraction
                    {
                        ActionLangCode = $"{Constants.ModId}:blockhelp-teleport-open",
                        MouseButton = EnumMouseButton.Right
                    })
                    .Append(new WorldInteraction
                    {
                        ActionLangCode = $"{Constants.ModId}:blockhelp-teleport-edit",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak"
                    });
            }

            return interactions;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            var beb = GetBehavior(world, blockSel.Position);
            beb?.Interact(world, byPlayer, ref handling);
            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }

        private static BlockEntityBehaviorTeleportLever? GetBehavior(IWorldAccessor world, BlockPos pos)
        {
            var be = world.BlockAccessor.GetBlockEntity(pos);
            if (be != null)
            {
                return be.GetBehavior<BlockEntityBehaviorTeleportLever>();
            }
            return null;
        }
    }
}
