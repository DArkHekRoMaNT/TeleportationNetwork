using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class TeleportMapLayer : MapLayer
    {
        public Dictionary<string, LoadedTexture> TexturesByIcon => _waypointMapLayer.texturesByIcon;
        public List<int> WaypointColors => _waypointMapLayer.WaypointColors;
        public MeshRef QuadModel => _waypointMapLayer.quadModel;
        public string[] WaypointIcons => _waypointMapLayer.WaypointIcons.Select(e => e.Key).ToArray();

        public new bool Active => base.Active && Core.Config.ShowTeleportOnMap;

        public override string Title => "TpNetOverlay";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        public override string LayerGroupCode => "tpnetwaypoints";

        private readonly List<MapComponent> _components = [];
        private readonly WaypointMapLayer _waypointMapLayer;

        public TeleportMapLayer(ICoreClientAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            var manager = api.ModLoader.GetModSystem<TeleportManager>();
            manager.Points.Changed += RebuildMapComponents;

            var mapLayers = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers;
            _waypointMapLayer = (WaypointMapLayer)mapLayers.First(ml => ml is WaypointMapLayer);
        }

        public override void Render(GuiElementMap mapElem, float dt)
        {
            if (!Active)
            {
                return;
            }

            foreach (var component in _components)
            {
                component.Render(mapElem, dt);
            }
        }

        public override void OnMapOpenedClient()
        {
            RebuildMapComponents();
        }

        private void RebuildMapComponents()
        {
            if (!mapSink.IsOpened)
            {
                return;
            }

            foreach (var component in _components.Cast<TeleportMapComponent>())
            {
                component.Dispose();
            }

            _components.Clear();

            var manager = api.ModLoader.GetModSystem<TeleportManager>();
            foreach (var teleport in manager.Points)
            {
                _components.Add(new TeleportMapComponent((ICoreClientAPI)api, teleport, this));
            }
        }

        public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            if (!Active)
            {
                return;
            }

            foreach (var component in _components)
            {
                component.OnMouseMove(args, mapElem, hoverText);
            }
        }

        public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem)
        {
            if (!Active)
            {
                return;
            }

            foreach (var component in _components)
            {
                component.OnMouseUpOnElement(args, mapElem);
                if (args.Handled)
                {
                    break;
                }
            }
        }
    }
}
