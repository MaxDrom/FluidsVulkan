using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public interface IGpuTask
{
    List<IComputeResource> Reads { get; }
    List<IComputeResource> Writes { get; }

    PipelineStageFlags PipelineStage { get; }

    PipelineStageFlags InvokeRecord(VkCommandRecordingScope scope);
}

public interface IComputeTask : IGpuTask
{
}