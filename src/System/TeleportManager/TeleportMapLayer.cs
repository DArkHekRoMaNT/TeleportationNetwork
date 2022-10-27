using Cairo;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class TeleportMapLayer : MapLayer
    {
        private readonly List<MapComponent> _components = new();

        public Dictionary<string, LoadedTexture> TexturesByIcon { get; } = new();
        public List<string> WaypointIcons { get; set; } = new()
        {
            "circle",
            "bee",
            "cave",
            "home",
            "ladder",
            "pick",
            "rocks",
            "ruins",
            "spiral",
            "star1",
            "star2",
            "trader",
            "vessel"
        };

        public MeshRef QuadModel { get; private set; }

        public override string Title => "TpNetOverlay";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        public TeleportMapLayer(ICoreClientAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            var manager = api.ModLoader.GetModSystem<TeleportManager>();
            manager.Points.Changed += RebuildMapComponents;

            QuadModel = api.Render.UploadMesh(QuadMeshUtil.GetQuad());
        }

        public override void Render(GuiElementMap mapElem, float dt)
        {
            foreach (var component in _components)
            {
                component.Render(mapElem, dt);
            }
        }

        public override void OnMapOpenedClient()
        {
            if (TexturesByIcon.Count == 0)
            {
                if (TexturesByIcon.Count > 0)
                {
                    foreach (var val in TexturesByIcon)
                    {
                        val.Value.Dispose();
                    }
                }

                double scale = RuntimeEnv.GUIScale;
                int size = (int)(27 * scale);

                var surface = new ImageSurface(Format.Argb32, size, size);
                var ctx = new Context(surface);

                ICoreClientAPI capi = (ICoreClientAPI)api;

                foreach (var val in WaypointIcons)
                {
                    ctx.Operator = Operator.Clear;
                    ctx.SetSourceRGBA(0, 0, 0, 0);
                    ctx.Paint();
                    ctx.Operator = Operator.Over;

                    capi.Gui.Icons.DrawIcon(ctx, "wp" + val.UcFirst(), 1, 1, size - 2, size - 2, new double[] { 0, 0, 0, 1 });
                    capi.Gui.Icons.DrawIcon(ctx, "wp" + val.UcFirst(), 2, 2, size - 4, size - 4, ColorUtil.WhiteArgbDouble);

                    TexturesByIcon[val] = new LoadedTexture(capi, capi.Gui.LoadCairoTexture(surface, false), (int)(20 * scale), (int)(20 * scale));
                }

                ctx.Dispose();
                surface.Dispose();
            }


            RebuildMapComponents();
        }

        private void RebuildMapComponents()
        {
            if (!mapSink.IsOpened) return;

            foreach (var component in _components.Cast<TeleportMapComponent>())
            {
                component.Dispose();
            }

            _components.Clear();

            var manager = api.ModLoader.GetModSystem<TeleportManager>();
            foreach (var teleport in manager.Points.GetAll())
            {
                _components.Add(new TeleportMapComponent((ICoreClientAPI)api, teleport, this));
            }
        }

        public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            foreach (var component in _components)
            {
                component.OnMouseMove(args, mapElem, hoverText);
            }
        }
    }
}
