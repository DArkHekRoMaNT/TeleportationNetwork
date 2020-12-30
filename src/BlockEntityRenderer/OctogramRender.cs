using System.Security.Cryptography;
using System.Xml.Schema;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class OctogramRender : IRenderer
    {
        private ICoreClientAPI api;
        private BlockPos pos;

        public float AngleRad;

        MeshRef meshref;
        public Matrixf ModelMat = new Matrixf();

        public OctogramRender(ICoreClientAPI capi, BlockPos pos, MeshData mesh)
        {
            this.api = capi;
            this.pos = pos;
            meshref = capi.Render.UploadMesh(mesh);
        }
        public double RenderOrder { get { return 0.5f; } }
        public int RenderRange => 24;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshref == null) return;

            AngleRad = deltaTime * 40 * GameMath.DEG2RAD;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(0.5f, 1.001f, 0.5f)
                .RotateY(AngleRad)
                .Translate(-0.5f, 0, -0.5f)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            rpi.RenderMesh(meshref);
            prog.Stop();
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            meshref.Dispose();
        }

    }
}