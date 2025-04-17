using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public class ImageResource : IComputeResource, IEquatable<ImageResource>
{
    public VkImage Image { get; init; }
    public AccessFlags AccessFlags { get; init; }
    public ImageLayout Layout { get; init; }
    
    public bool IsOverlap(IComputeResource other)
    {
        if (other is ImageResource otherImageResource)
            return otherImageResource.Image == Image;
        return false;
    }

    public T Accept<T>(IComputeResourceVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }

    public bool Equals(ImageResource other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Image.Equals(other.Image);
    }

    public bool Equals(IComputeResource other)
    {
        return Equals(other as ImageResource);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ImageResource)obj);
    }

    public override int GetHashCode()
    {
        return Image.GetHashCode();
    }

    public static bool operator ==(ImageResource left, ImageResource right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ImageResource left, ImageResource right)
    {
        return !Equals(left, right);
    }
}