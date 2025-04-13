using Silk.NET.Vulkan;

namespace FluidsVulkan;

public class UniformDescriptionAttribute(int binding,
    ShaderStageFlags shaderStageFlags,
    DescriptorType descriptorType,
    int descriptorCount
)
    : Attribute
{
    public int Binding { get; set; } = binding;

    public int DescriptorCount { get; set; } = descriptorCount;

    public ShaderStageFlags ShaderStageFlags { get; set; } = shaderStageFlags;

    public DescriptorType DescriptorType { get; set; } = descriptorType;
}