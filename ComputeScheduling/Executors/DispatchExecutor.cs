using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling.Executors;

public interface IDispatchExecutor
{

    void RecordDispatch(VkCommandRecordingScope scope);
}

public class DispatchExecutor<T> : IDispatchExecutor
    where T : unmanaged
{
    public uint NumGroupsX { get; init; }
    public uint NumGroupsY { get; init; }
    public uint NumGroupsZ { get; init; }

    public T PushConstant
    {
        get => _pushConstant;
        init => _pushConstant = value;
    }
    private T _pushConstant;
    
    public VkComputePipeline Pipeline { get;  init; }
    public DescriptorSet DescriptorSet { get; init; }


    public void RecordDispatch(VkCommandRecordingScope scope)
    {
        
        scope.BindPipeline(Pipeline);
        scope.BindDescriptorSets(PipelineBindPoint.Compute,
            Pipeline.PipelineLayout, [DescriptorSet]);
        scope.SetPushConstant(Pipeline,
            ShaderStageFlags.ComputeBit, ref _pushConstant);
        scope.Dispatch(NumGroupsX, NumGroupsY,
            NumGroupsZ);
    }
}