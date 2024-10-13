using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public class TeleportRenderSystem : ModSystem
    {
        public IShaderProgram Prog { get; private set; } = null!;

        private ICoreClientAPI _api = null!;

        public override void StartClientSide(ICoreClientAPI api)
        {
            _api = api;
            api.Event.ReloadShader += LoadShader;
            LoadShader();
        }

        public bool LoadShader()
        {
            Prog = _api.Shader.NewShaderProgram();
            Prog.VertexShader = _api.Shader.NewShader(EnumShaderType.VertexShader);
            Prog.FragmentShader = _api.Shader.NewShader(EnumShaderType.FragmentShader);
            _api.Shader.RegisterFileShaderProgram("teleport", Prog);
            return Prog.Compile();
        }
    }
}
