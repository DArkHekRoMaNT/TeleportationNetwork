using System;
using System.Linq;
using SharedUtils;
using SharedUtils.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class GuiDialogTeleport : GuiDialogGeneric
    {
        public TPNetManager TPNetManager { get; private set; }

        public bool IsDuplicate { get; }
        public override bool PrefersUngrabbedMouse => false;
        public override bool UnregisterOnClose => true;

        readonly BlockPos blockEntityPos;
        private long listenerId;

        public GuiDialogTeleport(ICoreClientAPI capi, BlockPos ownBEPos)
            : base(Lang.Get(ConstantsCore.ModId + ":tpdlg-title"), capi)
        {
            TPNetManager = capi.ModLoader.GetModSystem<TPNetManager>();
            IsDuplicate = ownBEPos != null && capi.OpenedGuis.FirstOrDefault((dlg) => (dlg as GuiDialogTeleport)?.blockEntityPos == ownBEPos) != null;
            if (!IsDuplicate)
            {
                blockEntityPos = ownBEPos;
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

        private void OnNewScrollbarValue(float value)
        {
            ElementBounds bounds = SingleComposer.GetContainer("stacklist").Bounds;

            bounds.fixedY = 3 - value;
            bounds.CalcWorldBounds();
        }

        private void SetupDialog()

        {
            var systemTemporalStability = capi.ModLoader.GetModSystem<SystemTemporalStability>();
            double currStability = capi.World.Player.Entity.WatchedAttributes.GetDouble("temporalStability");
            bool unstablePlayer = capi.World.Config.GetBool("temporalStability", true) && Config.Current.StabilityConsumable.Val > currStability;
            bool unstableWorld = unstablePlayer || systemTemporalStability.StormData.nowStormActive || Config.Current.StabilityTeleportMode.Val == "always";



            var availableTeleports = TPNetManager.GetAvailableTeleports(capi.World.Player);

            ElementBounds[] buttons = new ElementBounds[availableTeleports?.Count() > 0 ? availableTeleports.Count() : 1];

            buttons[0] = ElementBounds.Fixed(0, 0, 300, 40);
            for (int i = 1; i < buttons.Length; i++)
            {
                buttons[i] = buttons[i - 1].BelowCopy(0, 1);
            }


            ElementBounds listBounds = ElementBounds.Fixed(0, 0, 302, 400 + (unstableWorld ? 20 : 0)).WithFixedPadding(1);
            listBounds.BothSizing = ElementSizing.Fixed;

            ElementBounds messageBounds = listBounds.BelowCopy(0, 10).WithFixedHeight(40);

            ElementBounds clipBounds = listBounds.ForkBoundingParent();
            ElementBounds insetBounds = listBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = insetBounds
                .CopyOffsetedSibling(insetBounds.fixedWidth + 3.0)
                .WithFixedWidth(GuiElementScrollbar.DefaultScrollbarWidth)
                .WithFixedPadding(GuiElementScrollbar.DeafultScrollbarPadding);


            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithFixedOffset(0, GuiStyle.TitleBarHeight);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, scrollbarBounds);


            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog;


            SingleComposer = capi.Gui
                .CreateCompo(ConstantsCore.ModId + "-teleport-dialog", dialogBounds)
                .AddDialogTitleBar(DialogTitle, CloseIconPressed)
                .AddDialogBG(bgBounds, false)
                .BeginChildElements(bgBounds)
            ;

            if (availableTeleports == null || availableTeleports.Count() == 0)
            {
                SingleComposer
                        .AddStaticText(
                        Lang.Get(ConstantsCore.ModId + ":tpdlg-empty"),
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
                    .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                    .AddHoverText("", CairoFont.WhiteDetailText(), 300, listBounds.FlatCopy(), "hovertext")
            ;

            if (unstableWorld)
            {
                SingleComposer.AddDynamicText(
                    Lang.Get(ConstantsCore.ModId + ":tpdlg-unstable"),
                    CairoFont.WhiteSmallText(),
                    EnumTextOrientation.Center,
                    messageBounds,
                    "message"
                );

                listenerId = capi.World.RegisterGameTickListener(OnGameTick, 200);
            }

            SingleComposer
                .EndChildElements()
            ;

            var hoverTextElem = SingleComposer.GetHoverText("hovertext");
            hoverTextElem.SetAutoWidth(true);

            var stacklist = SingleComposer.GetContainer("stacklist");

            for (int i = 0; i < buttons.Length; i++)
            {
                var tp = availableTeleports.ElementAt(i);
                if (tp.Value.Name == null) tp.Value.Name = "null";

                bool playerLowStability = capi.World.Player?.Entity?.GetBehavior<EntityBehaviorTemporalStabilityAffected>()?.OwnStability < 0.2;
                bool nowStormActive = capi.ModLoader.GetModSystem<SystemTemporalStability>().StormData.nowStormActive;

                var font = CairoFont.WhiteSmallText();

                stacklist.Add(new GuiElementTeleportButton(capi,
                    (nowStormActive || playerLowStability) ? tp.Value.Name.Shuffle(capi.World.Rand) : tp.Value.Name,
                    tp.Key,
                    tp.Value.Available ? font : font.WithColor(ColorUtil.Hex2Doubles("#c91a1a")),
                    CairoFont.WhiteSmallText(),
                    () => OnClickItem(tp.Key),
                    buttons[i],
                    EnumButtonStyle.Normal
                ));

                if (tp.Key == blockEntityPos)
                {
                    (stacklist.Elements.Last() as GuiElementTeleportButton).Enabled = false;
                }
            }

            SingleComposer.Compose();
            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)insetBounds.fixedHeight,
                (float)Math.Max(insetBounds.fixedHeight, listBounds.fixedHeight)
            );
        }

        private void OnGameTick(float dt)
        {
            var textComponent = SingleComposer.GetDynamicText("message");
            string newText = Lang.Get(ConstantsCore.ModId + ":tpdlg-unstable");
            if (capi.World.Rand.Next(0, 10) == 0) newText = newText.Shuffle(capi.World.Rand);
            textComponent.SetNewText(newText, forceRedraw: true);
        }

        private bool OnClickItem(BlockPos targetPos)
        {
            var data = TPNetManager.GetTeleport(targetPos);
            if (data == null)
            {
                TryClose();
                return false;
            }

            TPNetManager.TeleportTo(targetPos.ToVec3d(), blockEntityPos?.ToVec3d());

            TryClose();

            return true;
        }

        public override void OnMouseMove(MouseEvent args)
        {
            base.OnMouseMove(args);

            if (SingleComposer != null && SingleComposer.Bounds.PointInside(args.X, args.Y))
            {
                var stacklist = SingleComposer.GetContainer("stacklist");
                if (stacklist == null) return;


                if (stacklist.Elements.FirstOrDefault((elem) => elem.IsPositionInside(args.X, args.Y)) is GuiElementTeleportButton button)
                {

                    int x = button.TeleportPos.X - capi.World.DefaultSpawnPosition.XYZInt.X;
                    int y = button.TeleportPos.Y;
                    int z = button.TeleportPos.Z - capi.World.DefaultSpawnPosition.XYZInt.Z;

                    string text = $"{x}, {y}, {z}";

                    var hoverTextElem = SingleComposer.GetHoverText("hovertext");
                    hoverTextElem.SetNewText(text);
                }
                else
                {
                    var hoverTextElem = SingleComposer.GetHoverText("hovertext");
                    hoverTextElem.SetNewText("");
                }
            }
        }
        public override bool TryClose()
        {
            capi.World.UnregisterGameTickListener(listenerId);
            return base.TryClose();
        }
    }
}