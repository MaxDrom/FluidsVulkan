using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public interface IGpuTask
{
    List<IComputeResource> Reads { get; }
    List<IComputeResource> Writes { get; }
    
    PipelineStageFlags InvokeRecord(VkCommandRecordingScope scope);
    
    PipelineStageFlags PipelineStage { get; }
}

public interface IComputeTask : IGpuTask
{}
