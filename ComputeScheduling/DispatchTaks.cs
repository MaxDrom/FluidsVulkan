using FluidsVulkan.ComputeSchduling;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public class DispatchTaks : IComputeTask
{
    public object PushConstant { get; set; }
    public uint NumGroupsX { get; set; }
    public uint NumGroupsY { get; set; }
    public uint NumGroupsZ { get; set; }

    public DescriptorSet DescriptorSet { get; set; }


    public IComputeShader ComputeShader { get; set; }
    public List<IComputeResource> Reads { get; set; }
    public List<IComputeResource> Writes { get; set; }

    public void Accept(IComputeTaskVisitor visitor)
    {
        visitor.Visit(this);
    }

    public T Accept<T>(IComputeTaskVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }

    public PipelineStageFlags PipelineStage =>
        PipelineStageFlags.ComputeShaderBit;
}