using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public struct FillBufferTask : IComputeTask
{
    public List<IComputeResource> Reads { get; }
    public List<IComputeResource> Writes { get; }

    public PipelineStageFlags InvokeRecord(
        VkCommandRecordingScope scope)
    {
        throw new NotImplementedException();
    }

    public PipelineStageFlags PipelineStage { get; }
}