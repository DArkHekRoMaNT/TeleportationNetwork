using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class GuiDialogTeleport : GuiDialogGeneric
    {
        public bool IsDuplicate { get; }
        public override bool PrefersUngrabbedMouse => false;
        public BlockPos OwnBEPos { get; }

        TPNetManager manager;

        public GuiDialogTeleport(ICoreClientAPI capi, BlockPos ownBEPos)
            : base(Lang.Get("Teleport"), capi)
        {
            IsDuplicate = (capi.OpenedGuis.FirstOrDefault((object dlg) => ownBEPos != null && (dlg as GuiDialogTeleport)?.OwnBEPos == ownBEPos) != null);
            if (!IsDuplicate)
            {
                manager = capi.ModLoader.GetModSystem<TPNetManager>();
                OwnBEPos = ownBEPos;
            }
        }

        protected void CloseIconPressed() => TryClose();

        public override bool TryOpen()
        {
            if (IsDuplicate)
            {
                return false;
            }

            SetupDialog();
            return base.TryOpen();
        }

        private void onNewScrollbarValue(float value)
        {
            ElementBounds bounds = SingleComposer.GetContainer("stacklist").Bounds;

            bounds.fixedY = -value;
            bounds.CalcWorldBounds();
        }

        private void SetupDialog()
        {
            int availableCount = TPNetManager.AllTeleports.Count == 0 ? 1 : TPNetManager.AllTeleports.Count
            ElementBounds[] buttons = new ElementBounds[];

            buttons[0] = ElementBounds.Fixed(0, 0, 300, 30);
            for (int i = 1; i < buttons.Length; i++)
            {
                buttons[i] = buttons[i - 1].BelowCopy(0, 2);
            }


            ElementBounds listBounds = ElementBounds.Fixed(0, 0, 300, 400);
            listBounds.BothSizing = ElementSizing.Fixed;

            ElementBounds clipBounds = listBounds.ForkBoundingParent();
            ElementBounds insetBounds = listBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = ElementStdBounds.VerticalScrollbar(insetBounds);


            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithFixedOffset(0, GuiStyle.TitleBarHeight);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, scrollbarBounds);


            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog;


            SingleComposer = capi.Gui
                .CreateCompo("teleport-dialog", dialogBounds)
                .AddDialogTitleBarWithBg(Lang.Get("Available Points"), CloseIconPressed)
                .AddShadedDialogBG(bgBounds, false)
                .BeginChildElements(bgBounds)
            ;

            if (TPNetManager.AllTeleports.Count == 0)
            {
                SingleComposer
                        .AddStaticText(
                        Lang.Get("No available teleports"),
                        CairoFont.WhiteSmallText(),
                        buttons[0])
                    .EndChildElements()
                    .Compose()
                ;
                return;
            }

            SingleComposer
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddContainer(listBounds, "stacklist")
                    .EndClip()
                    .AddVerticalScrollbar(onNewScrollbarValue, scrollbarBounds, "scrollbar")
                .EndChildElements()
            ;

            var stacklist = SingleComposer.GetContainer("stacklist");
            for (int i = 0; i < TPNetManager.AllTeleports.Count; i++)
            {
                var tp = TPNetManager.AllTeleports.ElementAt(i);
                if (tp.Value.Name == null) tp.Value.Name = "null";

                stacklist.Add(new GuiElementTextButton(capi,
                    tp.Value.Name,
                    CairoFont.WhiteSmallText(),
                    CairoFont.WhiteSmallText(),
                    () => onClickItem(tp.Key),
                    buttons[i],
                    EnumButtonStyle.MainMenu
                ), i - 1);

                if (tp.Key == OwnBEPos)
                {
                    (stacklist.Elements[i] as GuiElementTextButton).Enabled = false;
                }
            }

            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)listBounds.fixedHeight,
                (float)(buttons.Last().fixedHeight + buttons.Last().fixedY)
            );

            SingleComposer.Compose();
        }

        private bool onClickItem(BlockPos targetPos)
        {
            var tp = TPNetManager.AllTeleports[targetPos];

            manager.TeleportTo(targetPos.ToVec3d(), OwnBEPos.ToVec3d());
            TryClose();

            return true;
        }
    }
}