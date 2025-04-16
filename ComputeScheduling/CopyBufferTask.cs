using FluidsVulkan.ComputeSchduling;
using Silk.NET.Vulkan;
using YamlDotNet.Core.Tokens;

namespace FluidsVulkan.ComputeScheduling;

public class CopyBufferTask(IVkBuffer source,
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
        AccessFlags = AccessFlags.TransferWriteBit, Buffer = destination
    };

    public IVkBuffer Source => _source.Buffer;
    public IVkBuffer Destination => _destination.Buffer;
    public ulong Size { get; private set; } = size;

    public ulong SrcOffset { get; private set; } = srcOffset;
    public ulong DstOffset { get; private set; } = dstOffset;

    public List<IComputeResource> Reads => [_source];
    public List<IComputeResource> Writes => [_destination];

    public void Accept(IComputeTaskVisitor visitor)
    {
        visitor.Visit(this);
    }

    public T Accept<T>(IComputeTaskVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }

    public PipelineStageFlags PipelineStage =>
        PipelineStageFlags.TransferBit;
}