using Silk.NET.Vulkan;

namespace FluidsVulkan.Vulkan;

public class VkSubpassInfo(PipelineBindPoint bindPoint,
    AttachmentReference[] colorAttachmentReferences
)
{
    public AttachmentReference[] ColorAttachmentReferences
    {
        get;
        private set;
    } = colorAttachmentReferences;

    public PipelineBindPoint BindPoint { get; private set; } =
        bindPoint;
}