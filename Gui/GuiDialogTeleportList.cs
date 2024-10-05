using Cairo;
using CommonLib.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class GuiDialogTeleportList(ICoreClientAPI capi, BlockPos? blockEntityPos) : GuiDialogGeneric(Lang.Get($"{Constants.ModId}:tpdlg-title"), capi)
    {
        private TeleportManager TeleportManager { get; } = capi.ModLoader.GetModSystem<TeleportManager>();
        private BlockPos? Pos { get; } = blockEntityPos;

        private long? _listenerId;

        private bool IsUnstableWorld { get; set; }
        private readonly List<Teleport> _allPoints = [];

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
                    TeleportManager.Mod.Logger.Warning("Using not-exists teleport at {0}, gui closed", Pos);
                    TryClose();
                }
            }

            GetStability();
            UpdatePoints();

            ComposeDialog();
            return base.TryOpen();
        }

        private void ComposeDialog()
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

            bool emptyList = _allPoints.Count == 0;

            SingleComposer = capi.Gui
                .CreateCompo($"{Constants.ModId}-teleport-dialog", dialogBounds)
                .AddDialogTitleBar(DialogTitle, () => TryClose())
                .AddDialogBG(bgBounds, false)
                .BeginChildElements(bgBounds)
                    .AddIf(emptyList)
                        .AddStaticText(Lang.Get($"{Constants.ModId}:tpdlg-empty"), CairoFont.WhiteSmallText(), buttons[0])
                    .EndIf()
                    .AddIf(!emptyList)
                        .BeginClip(clipBounds)
                            .AddInset(insetBounds, 3)
                            .AddContainer(listBounds, "stacklist")
                        .EndClip()
                        .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                        .AddHoverText("", CairoFont.WhiteDetailText(), 300, listBounds.FlatCopy(), "hoverText")
                        .AddIf(IsUnstableWorld)
                            .AddDynamicText(Lang.Get($"{Constants.ModId}:tpdlg-unstable"),
                                CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center),
                                messageBounds, "message")
                        .EndIf()
                    .EndIf()
                .EndChildElements();

            if (!emptyList)
            {
                if (IsUnstableWorld)
                {
                    _listenerId = capi.World.RegisterGameTickListener(OnTextUpdateTick, 200);
                }

                SingleComposer.GetHoverText("hoverText").SetAutoWidth(true);

                SetupTargetButtons(buttons);

                SingleComposer.Compose();

                SingleComposer.GetScrollbar("scrollbar").SetHeights(
                    (float)insetBounds.fixedHeight,
                    (float)Math.Max(insetBounds.fixedHeight, listBounds.fixedHeight));
            }
            else
            {
                SingleComposer.Compose();
            }
        }

        private void SetupTargetButtons(ElementBounds[] buttons)
        {
            var stacklist = SingleComposer.GetContainer("stacklist");
            for (int i = 0; i < buttons.Length; i++)
            {
                var tp = _allPoints.ElementAt(i);
                var data = tp.GetClientData(capi);

                string name = tp.Name ?? "null";

                var stabilityBehavior = capi.World.Player?.Entity?.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
                bool playerLowStability = stabilityBehavior?.OwnStability < Core.Config.StabilityConsumable;
                bool nowStormActive = capi.ModLoader.GetModSystem<SystemTemporalStability>().StormData.nowStormActive;
                name = (nowStormActive || playerLowStability) ? name.Shuffle(capi.World.Rand) : name;

                var nameFont = CairoFont.WhiteSmallText();
                bool activated = tp.ActivatedByPlayers.Contains(capi.World.Player!.PlayerUID);
                bool enabled = tp.Enabled;

                if (!enabled)
                {
                    if (!activated)
                    {
                        nameFont.Color = ColorUtil.Hex2Doubles("#c91a1a");
                    }
                    else
                    {
                        nameFont.Color = ColorUtil.Hex2Doubles("#c95a5a");
                    }
                }
                else if (!activated)
                {
                    nameFont.Color = ColorUtil.Hex2Doubles("#555555");
                }

                if (data.Pinned)
                {
                    nameFont.FontWeight = FontWeight.Bold;
                }

                stacklist.Add(new GuiElementTeleportButton(capi,
                    name,
                    nameFont,
                    data.Pinned ?
                        CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold) :
                        CairoFont.WhiteSmallText(),
                    () => OnTeleportButtonClick(tp.Pos),
                    buttons[i],
                    EnumButtonStyle.Normal)
                {
                    TeleportPos = tp.Pos,
                    Enabled = IsPointEnabled(tp)
                });
            }
        }

        private bool IsPointEnabled(Teleport teleport)
        {
            return teleport.Pos != Pos;
        }

        private void UpdatePoints()
        {
            _allPoints.Clear();

            IEnumerable<Teleport> Sort(IEnumerable<Teleport> points)
            {
                return points
                    .OrderBy(tp => tp.Name)
                    .OrderBy(tp => -tp.GetClientData(capi).SortOrder);
            }

            if (capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                _allPoints.AddRange(Sort(TeleportManager.Points.GetAll()));
            }
            else
            {
                _allPoints.AddRange(Sort(TeleportManager.Points.GetAll((tp) =>
                    tp.Enabled &&
                    tp.ActivatedByPlayers.Contains(capi.World.Player.PlayerUID))));
            }
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
            string newText = Lang.Get($"{Constants.ModId}:tpdlg-unstable");
            if (capi.World.Rand.Next(0, 10) == 0)
            {
                newText = newText.Shuffle(capi.World.Rand);
            }

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
            ShowTeleportInfo(args.X, args.Y);
        }

        private void ShowTeleportInfo(int XMousePos, int YMousePos)
        {
            if (SingleComposer != null && SingleComposer.Bounds.PointInside(XMousePos, YMousePos))
            {
                var stacklist = SingleComposer.GetContainer("stacklist");
                if (stacklist == null)
                {
                    return;
                }

                if (stacklist.Elements.FirstOrDefault((elem) => elem.IsPositionInside(XMousePos, YMousePos)) is GuiElementTeleportButton button)
                {
                    int x = button.TeleportPos.X - capi.World.DefaultSpawnPosition.XYZInt.X;
                    int y = button.TeleportPos.Y;
                    int z = button.TeleportPos.Z - capi.World.DefaultSpawnPosition.XYZInt.Z;

                    var sb = new StringBuilder();
                    sb.AppendLine($"{x}, {y}, {z}");

                    var teleport = TeleportManager.Points[button.TeleportPos];
                    if (teleport != null)
                    {
                        var data = teleport.GetClientData(capi);
                        if (data != null && !string.IsNullOrWhiteSpace(data.Note))
                        {
                            sb.Append(data.Note);
                        }
                    }

                    SingleComposer.GetHoverText("hoverText").SetNewText(sb.ToString());
                }
                else
                {
                    SingleComposer.GetHoverText("hoverText").SetNewText("");
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
