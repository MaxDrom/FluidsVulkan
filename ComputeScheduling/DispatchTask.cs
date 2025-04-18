using FluidsVulkan.ComputeScheduling.Executors;
using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public readonly struct DispatchTask : IComputeTask
{
    public IDispatchExecutor Executor { get; init; }
    public List<IComputeResource> Reads { get; init; }
    public List<IComputeResource> Writes { get; init; }


    public PipelineStageFlags InvokeRecord(
        VkCommandRecordingScope scope)
    {
        Executor.RecordDispatch(scope);
        return PipelineStageFlags.ComputeShaderBit;
    }

    public PipelineStageFlags PipelineStage =>
        PipelineStageFlags.ComputeShaderBit;
}