using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public readonly struct CopyBufferToImageTask(IVkBuffer buffer,
    VkImage image,
    BufferImageCopy[] regions
)
    : IComputeTask
{
    public List<IComputeResource> Reads { get; } =
    [
        new BufferResource
        {
            AccessFlags = AccessFlags.TransferReadBit,
            Buffer = buffer,
        },
    ];

    public List<IComputeResource> Writes { get; } =
    [
        new ImageResource
        {
            AccessFlags = AccessFlags.TransferWriteBit,
            Image = image,
            Layout = ImageLayout.TransferDstOptimal,
        },
    ];

    public PipelineStageFlags InvokeRecord(
        VkCommandRecordingScope scope)
    {
        scope.CopyBufferToImage(buffer, image, [.. regions]);
        return PipelineStageFlags.TransferBit;
    }

    public PipelineStageFlags PipelineStage =>
        PipelineStageFlags.TransferBit;
}