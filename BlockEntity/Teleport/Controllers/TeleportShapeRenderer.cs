using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportShapeRenderer
    {
        private readonly ICoreClientAPI _capi;
        private readonly BlockPos _pos;

        private readonly float _rotationDeg;
        private readonly float _size;
        private readonly Shape _staticShape;
        private readonly Shape _dynamicShape;
        private readonly Shape _rodShape;

        public TeleportShapeRenderer(ICoreClientAPI capi, BlockPos pos, GateSettings settings)
        {
            _capi = capi;
            _pos = pos;

            _rotationDeg = settings.Rotation;
            _size = settings.Size;

            _staticShape = _capi.Assets.Get<Shape>(settings.Shapes.Static).Clone();
            _dynamicShape = _capi.Assets.Get<Shape>(settings.Shapes.Dynamic).Clone();
            _rodShape = _capi.Assets.Get<Shape>(settings.Shapes.Rod).Clone();
        }

        public void Update(float dt, TeleportActivator status)
        {
        }

        public void Dispose()
        {
        }
    }
}
