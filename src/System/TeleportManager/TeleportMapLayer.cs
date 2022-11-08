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
        public string[] WaypointIcons { get; } = new string[]
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

        public List<int> WaypointColors { get; }
        private static readonly string[] _hexColors = new string[]
        {
            "#F9D0DC", "#F179AF", "#F15A4A", "#ED272A", "#A30A35", "#  FFDE98", "#EFFD5F", "#F6EA5E", "#FDBB3A", "#C8772E",
            "#F47832", "C3D941", "#9FAB3A", "#94C948", "#47B749", "#366E4F", "#516D66", "93D7E3", "#7698CF", "#20909E",
            "#14A4DD", "#204EA2", "#28417A", "#C395C4", "#92479B", "#8E007E", "#5E3896", "D9D4CE", "#AFAAA8", "#706D64",
            "#4F4C2B", "#BF9C86", "#9885530", "#5D3D21", "#FFFFFF", "#080504"
        };

        public MeshRef QuadModel { get; private set; }

        public override string Title => "TpNetOverlay";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        public TeleportMapLayer(ICoreClientAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            var manager = api.ModLoader.GetModSystem<TeleportManager>();
            manager.Points.Changed += RebuildMapComponents;

            QuadModel = api.Render.UploadMesh(QuadMeshUtil.GetQuad());

            WaypointColors = new List<int>();
            for (int i = 0; i < _hexColors.Length; i++)
            {
                WaypointColors.Add(ColorUtil.Hex2Int(_hexColors[i]));
            }
        }

        public override void Render(GuiElementMap mapElem, float dt)
        {
            if (Core.Config.ShowTeleportOnMap)
            {
                foreach (var component in _components)
                {
                    component.Render(mapElem, dt);
                }
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

        public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem)
        {
            foreach (var component in _components)
            {
                component.OnMouseUpOnElement(args, mapElem);
                if (args.Handled) break;
            }
        }
    }
}
