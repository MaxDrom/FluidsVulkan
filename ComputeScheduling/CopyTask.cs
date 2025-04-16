using FluidsVulkan.ComputeSchduling;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public class CopyBufferTask : IComputeTask
{
    private BufferResource _source;
    private BufferResource _destination;

    public IVkBuffer Source => _source.Buffer;
    public IVkBuffer Destination => _destination.Buffer;
    public ulong Size { get; set; }

    public ulong SrcOffset { get; set; }
    public ulong DstOffset { get; set; }

    public CopyBufferTask(IVkBuffer source,
        IVkBuffer destination,
        ulong size,
        ulong srcOffset = 0,
        ulong dstOffset = 0)
    {
        _source = new BufferResource()
        {
            AccessFlags = AccessFlags.TransferReadBit, Buffer = source
        };
        _destination = new BufferResource()
        {
            AccessFlags = AccessFlags.TransferWriteBit, Buffer = destination
        };
        Size = size;
        SrcOffset = srcOffset;
        DstOffset = dstOffset;
    }

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