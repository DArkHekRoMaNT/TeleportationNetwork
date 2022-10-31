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
        private readonly TeleportMapLayer _teleportLayer;
        private readonly Matrixf _mvMat = new();
        private readonly Vec4f _color = new();

        private Vec2f _viewPos = new();
        private bool _mouseOver;


        public Teleport Teleport { get; set; }
        public Vec3d MapPos => Teleport.Pos.ToVec3d().AddCopy(.5, 0, .5);

        public TeleportMapComponent(ICoreClientAPI capi, Teleport teleport, TeleportMapLayer teleportLayer) : base(capi)
        {
            Teleport = teleport;
            _teleportLayer = teleportLayer;

            if (Teleport.Enabled)
            {
                Teleport.Color = ColorUtil.Hex2Int("#FF23cca2");
            }
            else
            {
                // a 10, b 11, c 12, d 13, e 14, f 15
                Teleport.Color = ColorUtil.Hex2Int("#FF116651");
            }

            ColorUtil.ToRGBAVec4f(Teleport.Color, ref _color);
        }

        public override void Render(GuiElementMap map, float dt)
        {
            map.TranslateWorldPosToViewPos(MapPos, ref _viewPos);

            if (_viewPos.X < -10 || _viewPos.Y < -10 ||
                _viewPos.X > map.Bounds.OuterWidth + 10 || _viewPos.Y > map.Bounds.OuterHeight + 10)
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

            if (!_teleportLayer.TexturesByIcon.TryGetValue(Teleport.Icon, out LoadedTexture tex))
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
        }

        public override void OnMouseMove(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            var viewPos = new Vec2f();
            mapElem.TranslateWorldPosToViewPos(MapPos, ref viewPos);

            double x = viewPos.X + mapElem.Bounds.renderX;
            double y = viewPos.Y + mapElem.Bounds.renderY;

            double dX = args.X - x;
            double dY = args.Y - y;

            if (_mouseOver = Math.Abs(dX) < 8 && Math.Abs(dY) < 8)
            {
                string text = Teleport.Name;
                hoverText.AppendLine(text);
            }
        }

        public override void OnMouseUpOnElement(MouseEvent args, GuiElementMap mapElem)
        {
            base.OnMouseUpOnElement(args, mapElem);

            if (_mouseOver && capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                capi.ModLoader.GetModSystem<TeleportManager>().
                    TeleportPlayerTo(Teleport.Pos);
            }
        }
    }
}
