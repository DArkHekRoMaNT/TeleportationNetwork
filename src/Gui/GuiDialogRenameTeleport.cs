using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class GuiDialogRenameTeleport : GuiDialogGeneric
    {
        private TeleportManager TeleportManager { get; }
        private BlockPos Pos { get; }

        public GuiDialogRenameTeleport(BlockPos blockEntityPos, ICoreClientAPI capi)
            : base(Lang.Get("Rename"), capi)
        {
            Pos = blockEntityPos;
            TeleportManager = capi.ModLoader.GetModSystem<TeleportManager>();

            if (TeleportManager.Points[blockEntityPos] == null)
            {
                Core.ModLogger.Error("Unable to rename an unregistered teleport");
                Dispose();
                return;
            }
        }

        public override bool TryOpen()
        {
            if (Pos != null)
            {
                var identicalDlg = capi.OpenedGuis
                    .FirstOrDefault(dlg => (dlg as GuiDialogRenameTeleport)?.Pos == Pos);

                if (identicalDlg != null)
                {
                    return false;
                }
            }

            SetupDialog();
            return base.TryOpen();
        }

        private void SetupDialog()
        {
            float textInputWidth = 250f;
            ElementBounds textInputBounds = ElementBounds
                .Fixed(0.0, 15.0, 150.0, 25.0)
                .BelowCopy()
                .WithFixedWidth(textInputWidth);

            ElementBounds cancelButtonBounds = textInputBounds
                .BelowCopy(0.0, 20.0)
                .WithFixedSize(100.0, 20.0)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(10.0, 2.0);

            ElementBounds saveButtonBounds = cancelButtonBounds
                .FlatCopy()
                .WithFixedSize(100.0, 20.0)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(10.0, 2.0);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(60.0 + GuiStyle.DialogToScreenPadding, GuiStyle.DialogToScreenPadding);


            SingleComposer = capi.Gui.CreateCompo("blockentitytexteditordialog", dialogBounds)
                .AddShadedDialogBG(bgBounds).AddDialogTitleBar(DialogTitle, () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddTextInput(textInputBounds, null, CairoFont.WhiteSmallText(), "text")
                    .AddSmallButton(Lang.Get("Cancel"), TryClose, cancelButtonBounds)
                    .AddSmallButton(Lang.Get("Save"), OnButtonSave, saveButtonBounds)
                .EndChildElements()
                .Compose();


            var teleport = TeleportManager.Points[Pos];
            if (teleport == null)
            {
                Core.ModLogger.Warning("Renaming not-exists teleport at {0}, gui closed", Pos);
                TryClose();
            }
            else
            {
                SingleComposer.GetTextInput("text").SetValue(teleport.Name);
            }
        }

        private bool OnButtonSave()
        {
            string name = SingleComposer.GetTextInput("text").GetText();
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            capi.Network.SendBlockEntityPacket(Pos, Constants.ChangeTeleportNamePacketId, bytes);

            return TryClose();
        }
    }
}
