using Silk.NET.Vulkan;

namespace FluidsVulkan.Vulkan;

public struct VkShaderInfo
{
    public string EntryPoint { get; init; }

    public VkShaderModule ShaderModule { get; init; }

    public SpecializationInfo? SpecializationInfo { get; init; }

    public VkShaderInfo(VkShaderModule shaderModule,
        string entryPoint,
        SpecializationInfo? specializationInfo = null)
    {
        EntryPoint = entryPoint;
        ShaderModule = shaderModule;
        SpecializationInfo = specializationInfo;
    }
}