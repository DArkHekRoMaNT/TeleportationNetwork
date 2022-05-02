using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class GuiDialogRenameTeleport : GuiDialogGeneric
    {
        public Action OnCloseCancel;

        ITeleportManager TeleportManager { get; }

        readonly BlockPos blockEntityPos;
        bool didSave;

        public GuiDialogRenameTeleport(BlockPos blockEntityPos, ICoreClientAPI capi)
            : base(Lang.Get("Rename"), capi)
        {
            this.blockEntityPos = blockEntityPos;

            TeleportManager = capi.ModLoader.GetModSystem<TeleportSystem>().Manager;

            if (blockEntityPos == null || TeleportManager.GetTeleport(blockEntityPos) == null)
            {
                Core.ModLogger.Error("Unable to rename an unregistered teleport");
                Dispose();
                return;
            }

            ElementBounds elementBounds = ElementBounds.Fixed(0.0, 0.0, 150.0, 20.0);
            ElementBounds elementBounds2 = ElementBounds.Fixed(0.0, 15.0, 150.0, 25.0);
            ElementBounds elementBounds3 = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            elementBounds3.BothSizing = ElementSizing.FitToChildren;
            ElementBounds bounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(60.0 + GuiStyle.DialogToScreenPadding, GuiStyle.DialogToScreenPadding);

            float num3 = 250f;
            SingleComposer = capi.Gui.CreateCompo("blockentitytexteditordialog", bounds).AddShadedDialogBG(elementBounds3).AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(elementBounds3)
                .AddTextInput(elementBounds2 = elementBounds2.BelowCopy().WithFixedWidth(num3), null, CairoFont.WhiteSmallText(), "text")
                .AddSmallButton(Lang.Get("Cancel"), OnButtonCancel, elementBounds2 = elementBounds2.BelowCopy(0.0, 20.0).WithFixedSize(100.0, 20.0).WithAlignment(EnumDialogArea.LeftFixed)
                    .WithFixedPadding(10.0, 2.0))
                .AddSmallButton(Lang.Get("Save"), OnButtonSave, elementBounds2 = elementBounds2.FlatCopy().WithFixedSize(100.0, 20.0).WithAlignment(EnumDialogArea.RightFixed)
                    .WithFixedPadding(10.0, 2.0))
                .EndChildElements()
                .Compose();

            SingleComposer.GetTextInput("text").SetValue(TeleportManager.GetTeleport(blockEntityPos).Name);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
        }

        private void OnTitleBarClose()
        {
            OnButtonCancel();
        }

        private bool OnButtonSave()
        {
            GuiElementTextInput textInput = base.SingleComposer.GetTextInput("text");

            Teleport teleport = TeleportManager.GetTeleport(blockEntityPos) as Teleport;
            teleport.Name = textInput.GetText();
            TeleportManager.SetTeleport(teleport);

            didSave = true;
            TryClose();
            return true;
        }

        private bool OnButtonCancel()
        {
            TryClose();
            return true;
        }

        public override void OnGuiClosed()
        {
            if (!didSave)
            {
                OnCloseCancel?.Invoke();
            }
            base.OnGuiClosed();
        }
    }
}
