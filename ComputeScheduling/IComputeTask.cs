using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public interface IComputeTask
{
    List<IComputeResource> Reads { get; }
    List<IComputeResource> Writes { get; }
    
    PipelineStageFlags InvokeRecord(VkCommandRecordingScope scope);
    
    PipelineStageFlags PipelineStage { get; }
}
