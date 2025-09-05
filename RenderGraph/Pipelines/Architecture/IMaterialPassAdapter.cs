using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.RenderGraph.Architecture;

public interface IMaterialPassAdapter<in TPass>
    where TPass : IPass
{
    VkGraphicsPipeline GetPipeline(TPass pass);
    void RecordDraw(VkCommandRecordingScope recordingScope, Rect2D scissor, TPass pass);
    void Set(string name, IVkBuffer buffer);
    void Set(string name, VkImage image);
    void SetPushConstant<TPush>(TPush value)
        where TPush : unmanaged;
}