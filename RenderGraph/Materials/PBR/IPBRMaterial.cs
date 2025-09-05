using FluidsVulkan.RenderGraph.Pipelines;

namespace FluidsVulkan.RenderGraph.Materials;

public interface IPBRMaterial : IMaterial,
    IMaterialForPass<DepthOnlyPass>, 
    IMaterialForPass<GBufferPass>
{ }

