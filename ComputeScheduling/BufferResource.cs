using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeSchduling;

public class BufferResource : IComputeResource
{
    public IVkBuffer Buffer { get; init; }
    public AccessFlags AccessFlags { get; init; }
    public bool IsOverlap(IComputeResource other)
    {
        if (other is BufferResource otherBufferResource)
            return otherBufferResource.Buffer == Buffer;
        return false;
    }

    public T Accept<T>(IComputeResourceVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }

    private bool Equals(BufferResource other)
    {
        return Buffer.Equals(other.Buffer);
    }

    public bool Equals(IComputeResource other)
    {
        return Equals(other as BufferResource);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((BufferResource)obj);
    }

    public override int GetHashCode()
    {
        return Buffer.GetHashCode();
    }

    public static bool operator ==(BufferResource left, BufferResource right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(BufferResource left, BufferResource right)
    {
        return !Equals(left, right);
    }
}