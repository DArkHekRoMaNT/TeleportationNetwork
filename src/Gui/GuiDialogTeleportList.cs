using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class GuiDialogTeleportList : GuiDialogGeneric
    {
        private TeleportManager TeleportManager { get; }
        private BlockPos? Pos { get; }

        private long? _listenerId;

        private bool IsUnstableWorld { get; set; }
        private List<Teleport> _allPoints;

        public GuiDialogTeleportList(ICoreClientAPI capi, BlockPos? blockEntityPos)
            : base(Lang.Get(Core.ModId + ":tpdlg-title"), capi)
        {
            Pos = blockEntityPos;
            TeleportManager = capi.ModLoader.GetModSystem<TeleportManager>();

            _allPoints = new();
        }

        public override bool TryOpen()
        {
            if (Pos != null)
            {
                var identicalDlg = capi.OpenedGuis
                    .FirstOrDefault(dlg => (dlg as GuiDialogTeleportList)?.Pos == Pos);

                if (identicalDlg != null)
                {
                    return false;
                }

                var teleport = TeleportManager.Points[Pos];
                if (teleport == null)
                {
                    Core.ModLogger.Warning("Using not-exists teleport at {0}, gui closed", Pos);
                    TryClose();
                }
            }

            GetStability();
            UpdatePoints();

            SetupDialog();
            return base.TryOpen();
        }

        private void SetupDialog()
        {
            ElementBounds[] buttons = new ElementBounds[_allPoints.Count > 0 ? _allPoints.Count : 1];

            buttons[0] = ElementBounds.Fixed(0, 0, 300, 40);
            for (int i = 1; i < buttons.Length; i++)
            {
                buttons[i] = buttons[i - 1].BelowCopy(0, 1);
            }

            ElementBounds listBounds = ElementBounds
                .Fixed(0, 0, 302, 400 + (IsUnstableWorld ? 20 : 0))
                .WithFixedPadding(1);

            listBounds.BothSizing = ElementSizing.Fixed;

            ElementBounds messageBounds = listBounds
                .BelowCopy(0, 10)
                .WithFixedHeight(40);

            ElementBounds clipBounds = listBounds.ForkBoundingParent();
            ElementBounds insetBounds = listBounds
                .FlatCopy()
                .FixedGrow(6)
                .WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = insetBounds
                .CopyOffsetedSibling(insetBounds.fixedWidth + 3.0)
                .WithFixedWidth(GuiElementScrollbar.DefaultScrollbarWidth)
                .WithFixedPadding(GuiElementScrollbar.DeafultScrollbarPadding);


            ElementBounds bgBounds = ElementBounds.Fill
                .WithFixedPadding(GuiStyle.ElementToDialogPadding)
                .WithFixedOffset(0, GuiStyle.TitleBarHeight);

            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, scrollbarBounds);


            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog;


            SingleComposer = capi.Gui
                        .CreateCompo(Core.ModId + "-teleport-dialog", dialogBounds)
                        .AddDialogTitleBar(DialogTitle, () => TryClose())
                        .AddDialogBG(bgBounds, false)
                        .BeginChildElements(bgBounds);

            if (_allPoints.Count == 0)
            {
                SingleComposer
                            .AddStaticText(Lang.Get(Core.ModId + ":tpdlg-empty"), CairoFont.WhiteSmallText(), buttons[0])
                        .EndChildElements()
                        .Compose();
            }
            else
            {
                SingleComposer
                        .BeginClip(clipBounds)
                            .AddInset(insetBounds, 3)
                            .AddContainer(listBounds, "stacklist")
                        .EndClip()
                        .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                        .AddHoverText("", CairoFont.WhiteDetailText(), 300, listBounds.FlatCopy(), "hovertext");

                if (IsUnstableWorld)
                {
                    SingleComposer
                        .AddDynamicText(Lang.Get(Core.ModId + ":tpdlg-unstable"),
                            CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center),
                            messageBounds, "message");

                    _listenerId = capi.World.RegisterGameTickListener(OnTextUpdateTick, 200);
                }

                SingleComposer
                        .EndChildElements();

                var hoverTextElem = SingleComposer.GetHoverText("hovertext");
                hoverTextElem.SetAutoWidth(true);

                SetupTargetButtons(buttons);

                SingleComposer
                         .Compose();


                SingleComposer.GetScrollbar("scrollbar").SetHeights(
                    (float)insetBounds.fixedHeight,
                    (float)Math.Max(insetBounds.fixedHeight, listBounds.fixedHeight));
            }
        }

        private void SetupTargetButtons(ElementBounds[] buttons)
        {
            var stacklist = SingleComposer.GetContainer("stacklist");
            for (int i = 0; i < buttons.Length; i++)
            {
                var tp = _allPoints.ElementAt(i);

                string name = tp.Name ?? "null";

                bool playerLowStability = capi.World.Player?.Entity?.GetBehavior<EntityBehaviorTemporalStabilityAffected>()?.OwnStability < 0.2;
                bool nowStormActive = capi.ModLoader.GetModSystem<SystemTemporalStability>().StormData.nowStormActive;
                name = (nowStormActive || playerLowStability) ? name.Shuffle(capi.World.Rand) : name;

                var nameFont = CairoFont.WhiteSmallText();
                bool activated = tp.ActivatedByPlayers.Contains(capi.World.Player!.PlayerUID);
                bool enabled = tp.Enabled;

                if (!enabled)
                {
                    if (!activated)
                    {
                        nameFont = nameFont.WithColor(ColorUtil.Hex2Doubles("#c91a1a"));
                    }
                    else
                    {

                        nameFont = nameFont.WithColor(ColorUtil.Hex2Doubles("#c95a5a"));
                    }
                }
                else if (!activated)
                {
                    nameFont = nameFont.WithColor(ColorUtil.Hex2Doubles("#555555"));
                }

                stacklist.Add(new GuiElementTeleportButton(capi,
                    name, tp.Pos, nameFont,
                    CairoFont.WhiteSmallText(),
                    () => OnTeleportButtonClick(tp.Pos),
                    buttons[i],
                    EnumButtonStyle.Normal)
                {
                    Enabled = CheckPointEnabled(tp)
                });
            }
        }

        private bool CheckPointEnabled(Teleport teleport)
        {
            return teleport.Pos != Pos;
        }

        private void UpdatePoints()
        {
            _allPoints.Clear();

            if (capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                _allPoints = TeleportManager.Points.GetAll().ToList();
            }
            else
            {
                _allPoints = TeleportManager.Points.GetAll((tp) =>
                    tp.Enabled &&
                    tp.ActivatedByPlayers.Contains(capi.World.Player.PlayerUID)
                ).ToList();
            }

            //if (Config.Current.SubNetworks)
            //{
            //    if (Config.Current.SharedTeleports)
            //    {
            //        availablePoints = TeleportManager.GetAllEnabledNeighbours(teleport);
            //    }
            //    else
            //    {
            //        availablePoints = TeleportManager
            //            .GetAllEnabledNeighboursActivatedByPlayer(teleport, capi.World.Player);
            //    }
            //}
            //else
            //{
            //    if (Config.Current.SharedTeleports)
            //    {
            //        availablePoints = TeleportManager.GetAllEnabledTeleports();
            //    }
            //    else
            //    {
            //        availablePoints = TeleportManager.GetAllEnabledActivatedByPlayer(capi.World.Player);
            //    }
            //}
        }

        private void GetStability()
        {
            var systemTemporalStability = capi.ModLoader.GetModSystem<SystemTemporalStability>();
            double currStability = capi.World.Player.Entity.WatchedAttributes.GetDouble("temporalStability");
            bool unstablePlayer = capi.World.Config.GetBool("temporalStability", true) && Core.Config.StabilityConsumable > currStability;
            IsUnstableWorld = unstablePlayer || systemTemporalStability.StormData.nowStormActive || Core.Config.StabilityTeleportMode == "always";
        }

        private void OnNewScrollbarValue(float value)
        {
            ElementBounds bounds = SingleComposer.GetContainer("stacklist").Bounds;

            bounds.fixedY = 3 - value;
            bounds.CalcWorldBounds();
        }

        private void OnTextUpdateTick(float dt)
        {
            var textComponent = SingleComposer.GetDynamicText("message");
            string newText = Lang.Get(Core.ModId + ":tpdlg-unstable");
            if (capi.World.Rand.Next(0, 10) == 0) newText = newText.Shuffle(capi.World.Rand);
            textComponent.SetNewText(newText, forceRedraw: true);
        }

        private bool OnTeleportButtonClick(BlockPos targetPoint)
        {
            if (Pos != null)
            {
                using var ms = new MemoryStream();
                using var writer = new BinaryWriter(ms);
                targetPoint.ToBytes(writer);
                capi.Network.SendBlockEntityPacket(Pos, Constants.TeleportPlayerPacketId, ms.ToArray());
            }
            else
            {
                TeleportManager.TeleportPlayerTo(targetPoint);
            }

            return TryClose();
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
            if (_listenerId != null)
            {
                capi.World.UnregisterGameTickListener((long)_listenerId);
            }
            return base.TryClose();
        }
    }
}
