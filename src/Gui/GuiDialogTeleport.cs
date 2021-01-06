using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class GuiDialogTeleport : GuiDialogGeneric
    {
        public bool IsDuplicate { get; }
        public override bool PrefersUngrabbedMouse => false;
        public override bool UnregisterOnClose => true;
        public BlockPos OwnBEPos { get; }

        TPNetManager manager;

        public GuiDialogTeleport(ICoreClientAPI capi, BlockPos ownBEPos)
            : base(Lang.Get("Available Points"), capi)
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

            bounds.fixedY = 3 - value;
            bounds.CalcWorldBounds();
        }

        private void SetupDialog()
        {
            Dictionary<BlockPos, TeleportData> availableTps = TPNetManager.GetAvailableTeleportsWithData(capi.World.Player);

            ElementBounds[] buttons = new ElementBounds[availableTps?.Count() > 0 ? availableTps.Count() : 1];

            buttons[0] = ElementBounds.Fixed(0, 0, 300, 40);
            for (int i = 1; i < buttons.Length; i++)
            {
                buttons[i] = buttons[i - 1].BelowCopy(0, 1);
            }


            ElementBounds listBounds = ElementBounds.Fixed(0, 0, 302, 400).WithFixedPadding(1);
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
                .AddDialogTitleBar(DialogTitle, CloseIconPressed)
                .AddDialogBG(bgBounds, false)
                .BeginChildElements(bgBounds)
            ;

            if (availableTps == null || availableTps.Count == 0)
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

            for (int i = 0; i < buttons.Length; i++)
            {
                var tp = availableTps.ElementAt(i);
                if (tp.Value.Name == null) tp.Value.Name = "null";

                // string tpName = tp.Value.Name + "\n\r" +
                //     "[ " + (tp.Key.X - capi.World.DefaultSpawnPosition.XYZInt.X) + ", " +
                //     (tp.Key.Z - capi.World.DefaultSpawnPosition.XYZInt.Z) + " ]";

                string tpName = tp.Value.Name;

                stacklist.Add(new GuiElementTextButton(capi,
                    tpName,
                    CairoFont.WhiteSmallText(),
                    CairoFont.WhiteSmallText(),
                    () => onClickItem(tp.Key),
                    buttons[i],
                    EnumButtonStyle.Normal
                ));

                if (tp.Key == OwnBEPos)
                {
                    (stacklist.Elements.Last() as GuiElementTextButton).Enabled = false;
                }
            }

            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)Math.Min(listBounds.fixedHeight, (buttons.Last().fixedHeight + buttons.Last().fixedY)),
                (float)(buttons.Last().fixedHeight + buttons.Last().fixedY)
                );
            //SingleComposer.GetScrollbar("scrollbar").ScrollToBottom();
            //SingleComposer.GetScrollbar("scrollbar").CurrentYPosition = 0;
            SingleComposer.Compose();
        }

        private bool onClickItem(BlockPos targetPos)
        {
            var tp = TPNetManager.AllTeleports[targetPos];

            manager.TeleportTo(targetPos.ToVec3d(), OwnBEPos?.ToVec3d());
            TryClose();

            return true;
        }
    }
}