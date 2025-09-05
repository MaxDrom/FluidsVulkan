namespace FluidsVulkan.RenderGraph.Architecture;

internal interface IMaterialForPass<out TPass>
    where TPass : IPass
{
    TPass GetPassAdapter();
}