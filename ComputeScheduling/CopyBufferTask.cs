using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public readonly struct CopyBufferTask(IVkBuffer source,
    IVkBuffer destination,
    ulong size,
    ulong srcOffset = 0,
    ulong dstOffset = 0
)
    : IComputeTask
{
    private readonly BufferResource _source = new()
    {
        AccessFlags = AccessFlags.TransferReadBit, Buffer = source,
    };

    private readonly BufferResource _destination = new()
    {
        AccessFlags = AccessFlags.TransferWriteBit,
        Buffer = destination,
    };

    public List<IComputeResource> Reads => [_source];
    public List<IComputeResource> Writes => [_destination];

    public PipelineStageFlags InvokeRecord(
        VkCommandRecordingScope scope)
    {
        scope.CopyBuffer(_source.Buffer, _destination.Buffer,
            srcOffset, dstOffset, size);

        return PipelineStageFlags.TransferBit;
    }

    public PipelineStageFlags PipelineStage =>
        PipelineStageFlags.TransferBit;
}