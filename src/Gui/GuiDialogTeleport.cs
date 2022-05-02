using SharedUtils.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class GuiDialogTeleport : GuiDialogGeneric
    {
        public ITeleportManager TeleportManager { get; private set; }

        public bool IsDuplicate { get; }
        public override bool PrefersUngrabbedMouse => false;
        public override bool UnregisterOnClose => true;

        readonly BlockPos blockEntityPos;
        private long? listenerId;

        private bool unstableWorld;
        private List<ITeleport> allPoints;
        private List<ITeleport> availablePoints;

        public GuiDialogTeleport(ICoreClientAPI capi, BlockPos ownBEPos)
            : base(Lang.Get(Core.ModId + ":tpdlg-title"), capi)
        {
            TeleportManager = capi.ModLoader.GetModSystem<TeleportSystem>().Manager;

            IsDuplicate = ownBEPos != null && capi.OpenedGuis
                .FirstOrDefault((dlg) => (dlg as GuiDialogTeleport)?.blockEntityPos == ownBEPos) != null;

            if (!IsDuplicate)
            {
                blockEntityPos = ownBEPos;
            }
        }

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
            GetStability();
            if (!TryGetAvailablePoints())
            {
                TryClose();
                return;
            }

            ElementBounds[] buttons = new ElementBounds[allPoints?.Count > 0 ? allPoints.Count : 1];

            buttons[0] = ElementBounds.Fixed(0, 0, 300, 40);
            for (int i = 1; i < buttons.Length; i++)
            {
                buttons[i] = buttons[i - 1].BelowCopy(0, 1);
            }

            ElementBounds listBounds = ElementBounds
                .Fixed(0, 0, 302, 400 + (unstableWorld ? 20 : 0))
                .WithFixedPadding(1);

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
                .CreateCompo(Core.ModId + "-teleport-dialog", dialogBounds)
                .AddDialogTitleBar(DialogTitle, () => TryClose())
                .AddDialogBG(bgBounds, false)
                .BeginChildElements(bgBounds);

            if (allPoints == null || allPoints.Count == 0)
            {
                SingleComposer
                        .AddStaticText(Lang.Get(Core.ModId + ":tpdlg-empty"),
                            CairoFont.WhiteSmallText(), buttons[0])
                    .EndChildElements()
                    .Compose();
                return;
            }

            SingleComposer
                .BeginClip(clipBounds)
                    .AddInset(insetBounds, 3)
                    .AddContainer(listBounds, "stacklist")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                .AddHoverText("", CairoFont.WhiteDetailText(), 300, listBounds.FlatCopy(), "hovertext");

            if (unstableWorld)
            {
                SingleComposer
                    .AddDynamicText(Lang.Get(Core.ModId + ":tpdlg-unstable"),
                        CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center),
                        messageBounds, "message");

                listenerId = capi.World.RegisterGameTickListener(OnTextUpdateTick, 200);
            }

            SingleComposer
                .EndChildElements();

            var hoverTextElem = SingleComposer.GetHoverText("hovertext");
            hoverTextElem.SetAutoWidth(true);

            SetupTargetButtons(buttons);

            SingleComposer.Compose();
            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)insetBounds.fixedHeight,
                (float)Math.Max(insetBounds.fixedHeight, listBounds.fixedHeight)
            );
        }

        private void SetupTargetButtons(ElementBounds[] buttons)
        {
            var stacklist = SingleComposer.GetContainer("stacklist");
            for (int i = 0; i < buttons.Length; i++)
            {
                var tp = allPoints.ElementAt(i);

                string name = tp.Name;
                if (name == null)
                {
                    name = "null";
                }

                bool playerLowStability = capi.World.Player?.Entity?.GetBehavior<EntityBehaviorTemporalStabilityAffected>()?.OwnStability < 0.2;
                bool nowStormActive = capi.ModLoader.GetModSystem<SystemTemporalStability>().StormData.nowStormActive;
                name = (nowStormActive || playerLowStability) ? name.Shuffle(capi.World.Rand) : name;


                var nameFont = CairoFont.WhiteSmallText();

                if (!tp.Enabled)
                {
                    nameFont = nameFont.WithColor(ColorUtil.Hex2Doubles("#c91a1a"));
                }

                if (!tp.ActivatedByPlayers.Contains(capi.World.Player.PlayerUID))
                {
                    nameFont = nameFont.WithSlant(Cairo.FontSlant.Oblique);
                }

                stacklist.Add(new GuiElementTeleportButton(capi, name, tp.Pos,
                     nameFont, CairoFont.WhiteSmallText(), () => OnTeleportButtonClick(tp.Pos),
                     buttons[i], EnumButtonStyle.Normal)
                {
                    Enabled = availablePoints.Contains(tp)
                });
            }
        }

        private bool TryGetAvailablePoints()
        {
            allPoints = null;
            availablePoints = null;
            ITeleport teleport = TeleportManager.GetTeleport(blockEntityPos);

            if (capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                allPoints = TeleportManager.GetAllTeleports();
                availablePoints = allPoints.ToList();

                if (teleport != null)
                {
                    availablePoints.Remove(teleport);
                }

                return true;
            }
            else if (teleport == null)
            {
                return false;
            }

            if (Config.Current.SubNetworks)
            {
                if (Config.Current.SharedTeleports)
                {
                    availablePoints = TeleportManager.GetAllEnabledNeighbours(teleport);
                }
                else
                {
                    availablePoints = TeleportManager
                        .GetAllEnabledNeighboursActivatedByPlayer(teleport, capi.World.Player);
                }
            }
            else
            {
                if (Config.Current.SharedTeleports)
                {
                    availablePoints = TeleportManager.GetAllEnabledTeleports();
                }
                else
                {
                    availablePoints = TeleportManager.GetAllEnabledActivatedByPlayer(capi.World.Player);
                }
            }

            allPoints = TeleportManager.GetAllEnabledActivatedByPlayer(capi.World.Player);
            return true;
        }

        private void GetStability()
        {
            var systemTemporalStability = capi.ModLoader.GetModSystem<SystemTemporalStability>();
            double currStability = capi.World.Player.Entity.WatchedAttributes.GetDouble("temporalStability");
            bool unstablePlayer = capi.World.Config.GetBool("temporalStability", true) && Config.Current.StabilityConsumable.Val > currStability;
            unstableWorld = unstablePlayer || systemTemporalStability.StormData.nowStormActive || Config.Current.StabilityTeleportMode.Val == "always";
        }

        private void OnTextUpdateTick(float dt)
        {
            var textComponent = SingleComposer.GetDynamicText("message");
            string newText = Lang.Get(Core.ModId + ":tpdlg-unstable");
            if (capi.World.Rand.Next(0, 10) == 0) newText = newText.Shuffle(capi.World.Rand);
            textComponent.SetNewText(newText, forceRedraw: true);
        }

        private bool OnTeleportButtonClick(BlockPos targetPos)
        {
            var data = TeleportManager.GetTeleport(targetPos);
            if (data == null)
            {
                TryClose();
                return false;
            }

            var teleportSystem = capi.ModLoader.GetModSystem<TeleportSystem>();
            var teleportClientNetwork = teleportSystem.Network as ITeleportNetworkClient;
            teleportClientNetwork.TeleportTo(targetPos.ToVec3d(), blockEntityPos?.ToVec3d());

            TryClose();

            return true;
        }

        public override void OnMouseMove(MouseEvent args)
        {
            base.OnMouseMove(args);

            ShowTeleportPos(args.X, args.Y);
        }

        private void ShowTeleportPos(int XMousePos, int YMousePos)
        {
            if (SingleComposer != null && SingleComposer.Bounds.PointInside(XMousePos, YMousePos))
            {
                var stacklist = SingleComposer.GetContainer("stacklist");
                if (stacklist == null) return;


                if (stacklist.Elements.FirstOrDefault((elem) => elem.IsPositionInside(XMousePos, YMousePos)) is GuiElementTeleportButton button)
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
            if (listenerId != null)
            {
                capi.World.UnregisterGameTickListener((long)listenerId);
            }
            return base.TryClose();
        }
    }
}