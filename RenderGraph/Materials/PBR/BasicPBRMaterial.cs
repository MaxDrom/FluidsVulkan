using FluidsVulkan.RenderGraph.Pipelines;

namespace FluidsVulkan.RenderGraph.Materials;

public class BasicPBRMaterial : IPBRMaterial
{
    IMaterialPassAdapter<DepthOnlyPass> IMaterialForPass<DepthOnlyPass>.GetPassAdapter()
    {
        throw new NotImplementedException();
    }

    IMaterialPassAdapter<GBufferPass> IMaterialForPass<GBufferPass>.GetPassAdapter()
    {
        throw new NotImplementedException();
    }
}