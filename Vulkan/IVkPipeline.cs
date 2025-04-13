using Silk.NET.Vulkan;

namespace FluidsVulkan;

public interface IVkPipeline
{
    Pipeline InternalPipeline { get; }
    PipelineBindPoint BindPoint { get; }
    PipelineLayout PipelineLayout { get; }
}