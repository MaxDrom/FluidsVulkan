using FluidsVulkan.ComputeScheduling;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeSchduling;

public interface IComputeTask
{
    List<IComputeResource> Reads { get; }
    List<IComputeResource> Writes { get; }
    
    void Accept(IComputeTaskVisitor visitor);

    T Accept<T>(IComputeTaskVisitor<T> visitor);
    
    PipelineStageFlags PipelineStage { get; }
}

public interface IComputeTaskVisitor
{
    void Visit(DispatchTaks resource);
    void Visit(CopyBufferTask resource);
}

public interface IComputeTaskVisitor<out T>
{
    T Visit(DispatchTaks resource);
    T Visit(CopyBufferTask resource);
}