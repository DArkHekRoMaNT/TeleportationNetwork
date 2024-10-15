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
using Vintagestory.API.Util;
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
            if (capi.OpenedGuis.Any(dlg => (dlg as GuiDialogTeleportList)?.Pos == Pos))
            {
                return false;
            }

            if (Pos != null)
            {
                if (!TeleportManager.Points.TryGetValue(Pos, out var teleport))
                {
                    TeleportManager.Mod.Logger.Warning($"Using not-exists teleport at {Pos}, gui closed");
                    TryClose();
                }
            }

            CheckStability();
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

                var stabilityBehavior = capi.World.Player?.Entity?.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
                var playerLowStability = stabilityBehavior?.OwnStability < Core.Config.StabilityConsumable;
                var nowStormActive = capi.ModLoader.GetModSystem<SystemTemporalStability>().StormData.nowStormActive;
                var name = (nowStormActive || playerLowStability) ? tp.Name.Shuffle(capi.World.Rand) : tp.Name;

                var nameFont = CairoFont.WhiteSmallText();
                var activated = tp.ActivatedByPlayers.Contains(capi.World.Player!.PlayerUID);
                var enabled = tp.Enabled;
                var linked = tp.Target == Pos;

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

                if (linked)
                {
                    nameFont.Color = ColorUtil.Hex2Doubles("#2CF5BA");
                    nameFont.FontWeight = FontWeight.Bold;
                }

                if (data.Pinned)
                {
                    nameFont.FontWeight = FontWeight.Bold;
                }

                stacklist.Add(new GuiElementTeleportButton(capi,
                    name,
                    nameFont,
                    nameFont,
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
            var showAll = capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative;
            _allPoints.Clear();
            _allPoints.AddRange(TeleportManager.Points
                .Where(tp => showAll || (tp.Enabled && tp.ActivatedByPlayers.Contains(capi.World.Player.PlayerUID)))
                .OrderBy(tp => tp.Name)
                .OrderBy(tp => -tp.GetClientData(capi).SortOrder));
        }

        private void CheckStability()
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
                if (TeleportManager.Points.TryGetValue(Pos, out var teleport))
                {
                    if (teleport.Target != targetPoint) // Not opened
                    {
                        using var ms = new MemoryStream();
                        using var writer = new BinaryWriter(ms);
                        targetPoint.ToBytes(writer);
                        capi.Network.SendBlockEntityPacket(Pos, Constants.OpenTeleportPacketId, ms.ToArray());
                    }
                    else
                    {
                        capi.Network.SendBlockEntityPacket(Pos, Constants.CloseTeleportPacketId);
                    }
                }
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

                    if (TeleportManager.Points.TryGetValue(button.TeleportPos, out var teleport))
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
