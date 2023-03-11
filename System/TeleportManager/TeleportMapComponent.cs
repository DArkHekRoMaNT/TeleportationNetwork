using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class TeleportMapComponent : MapComponent
    {
        private readonly Teleport _teleport;
        private readonly TeleportClientData _data;
        private readonly TeleportMapLayer _teleportLayer;
        private readonly Matrixf _mvMat = new();
        private readonly Vec4f _color = new();

        private Vec2f _viewPos = new();
        private bool _mouseOver;
        private GuiDialogEditTeleport? _editTeleportDialog;

        public Vec3d MapPos => _teleport.Pos.ToVec3d().AddCopy(.5, 0, .5);

        public TeleportMapComponent(ICoreClientAPI capi, Teleport teleport, TeleportMapLayer teleportLayer) : base(capi)
        {
            _teleport = teleport;
            _teleportLayer = teleportLayer;
            _data = _teleport.GetClientData(capi);

            if (_teleport.Enabled)
            {
                ColorUtil.ToRGBAVec4f(_data.Color, ref _color);
            }
            else
            {
                int color = ColorUtil.Hex2Int(Core.Config.BrokenTeleportColor);
                ColorUtil.ToRGBAVec4f(color, ref _color);
            }

            _color.A = 1;
        }

        public override void Render(GuiElementMap map, float dt)
        {
            map.TranslateWorldPosToViewPos(MapPos, ref _viewPos);
            if (_data.Pinned)
            {
                map.Api.Render.PushScissor(null);
                map.ClampButPreserveAngle(ref _viewPos, 2);
            }
            else if (_viewPos.X < -10 || _viewPos.Y < -10 ||
                _viewPos.X > map.Bounds.OuterWidth + 10 ||
                _viewPos.Y > map.Bounds.OuterHeight + 10)
            {
                return;
            }

            float x = (float)(map.Bounds.renderX + _viewPos.X);
            float y = (float)(map.Bounds.renderY + _viewPos.Y);

            ICoreClientAPI api = map.Api;

            IShaderProgram prog = api.Render.GetEngineShader(EnumShaderProgram.Gui);
            prog.Uniform("rgbaIn", _color);
            prog.Uniform("extraGlow", 0);
            prog.Uniform("applyColor", 0);
            prog.Uniform("noTexture", 0f);

            float hover = (_mouseOver ? 6 : 0) - 1.5f * Math.Max(1, 1 / map.ZoomLevel);

            if (!_teleportLayer.TexturesByIcon.TryGetValue(_data.Icon, out LoadedTexture tex))
            {
                _teleportLayer.TexturesByIcon.TryGetValue("circle", out tex);
            }

            if (tex != null)
            {
                prog.BindTexture2D("tex2d", tex.TextureId, 0);

                _mvMat
                    .Set(api.Render.CurrentModelviewMatrix)
                    .Translate(x, y, 60)
                    .Scale(tex.Width + hover, tex.Height + hover, 0)
                    .Scale(0.5f, 0.5f, 0);

                prog.UniformMatrix("projectionMatrix", api.Render.CurrentProjectionMatrix);
                prog.UniformMatrix("modelViewMatrix", _mvMat.Values);

                api.Render.RenderMesh(_teleportLayer.QuadModel);
            }

            if (_data.Pinned)
            {
                map.Api.Render.PopScissor();
            }
        }

        public override void OnMouseMove(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            var viewPos = new Vec2f();
            mapElem.TranslateWorldPosToViewPos(MapPos, ref viewPos);

            double x = viewPos.X + mapElem.Bounds.renderX;
            double y = viewPos.Y + mapElem.Bounds.renderY;

            if (_data.Pinned)
            {
                mapElem.ClampButPreserveAngle(ref viewPos, 2);
                x = viewPos.X + mapElem.Bounds.renderX;
                y = viewPos.Y + mapElem.Bounds.renderY;

                x = (float)GameMath.Clamp(x, mapElem.Bounds.renderX + 2, mapElem.Bounds.renderX + mapElem.Bounds.InnerWidth - 2);
                y = (float)GameMath.Clamp(y, mapElem.Bounds.renderY + 2, mapElem.Bounds.renderY + mapElem.Bounds.InnerHeight - 2);
            }
            double dX = args.X - x;
            double dY = args.Y - y;

            if (_mouseOver = Math.Abs(dX) < 8 && Math.Abs(dY) < 8)
            {
                string text = _teleport.Name;
                hoverText.AppendLine(text);

                if (!string.IsNullOrWhiteSpace(_data.Note))
                {
                    hoverText.AppendLine(_data.Note);
                }
            }
        }

        public override void OnMouseUpOnElement(MouseEvent args, GuiElementMap mapElem)
        {
            base.OnMouseUpOnElement(args, mapElem);

            if (_mouseOver)
            {
                if (args.Button == EnumMouseButton.Left &&
                    capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    capi.ModLoader.GetModSystem<TeleportManager>().TeleportPlayerTo(_teleport.Pos);
                }
                else if(args.Button == EnumMouseButton.Right)
                {
                    if (_editTeleportDialog != null)
                    {
                        _editTeleportDialog.TryClose();
                        _editTeleportDialog.Dispose();
                    }

                    var mapdlg = capi.ModLoader.GetModSystem<WorldMapManager>().worldMapDlg;
                    _editTeleportDialog = new GuiDialogEditTeleport(capi, _teleport.Pos, _teleportLayer);
                    _editTeleportDialog.TryOpen();
                    _editTeleportDialog.OnClosed += () => capi.Gui.RequestFocus(mapdlg);
                    args.Handled = true;
                }
            }
        }
    }
}
