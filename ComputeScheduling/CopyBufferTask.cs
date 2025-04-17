using FluidsVulkan.ComputeSchduling;
using FluidsVulkan.ComputeScheduling.Executors;
using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;
using YamlDotNet.Core.Tokens;

namespace FluidsVulkan.ComputeScheduling;

public struct CopyBufferTask(IVkBuffer source,
    IVkBuffer destination,
    ulong size,
    ulong srcOffset = 0,
    ulong dstOffset = 0
)
    : IComputeTask
{
    private BufferResource _source = new()
    {
        AccessFlags = AccessFlags.TransferReadBit, Buffer = source
    };

    private BufferResource _destination = new()
    {
        AccessFlags = AccessFlags.TransferWriteBit,
        Buffer = destination
    };
    
    public ulong Size { get; } = size;

    public ulong SrcOffset { get; } = srcOffset;
    public ulong DstOffset { get; } = dstOffset;

    public List<IComputeResource> Reads => [_source];
    public List<IComputeResource> Writes => [_destination];

    public PipelineStageFlags InvokeRecord(
        VkCommandRecordingScope scope)
    {
        scope.CopyBuffer(_source.Buffer, _destination.Buffer,
            SrcOffset, DstOffset, Size);

        return PipelineStageFlags.TransferBit;
    }

    public PipelineStageFlags PipelineStage =>
        PipelineStageFlags.TransferBit;
}