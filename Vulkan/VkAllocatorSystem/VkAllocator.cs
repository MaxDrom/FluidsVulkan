using Silk.NET.Vulkan;

namespace FluidsVulkan.Vulkan.VkAllocatorSystem;

public class AllocationNode(DeviceMemory deviceMemory, ulong offset)
{
    public DeviceMemory Memory { get; init; } = deviceMemory;

    public ulong Offset { get; init; } = offset;
    internal AllocationNode Next { get; set; }
}

public interface IVkAllocatorFactory
{
    VkAllocator Create(MemoryPropertyFlags requiredProperties,
        MemoryHeapFlags preferredFlags);
}

public abstract class VkAllocator(VkContext ctx,
    VkDevice device,
    MemoryPropertyFlags requiredProperties,
    MemoryHeapFlags preferredFlags
)
    : IDisposable
{
    private bool _disposedValue;
    protected MemoryHeapFlags PreferredFlags = preferredFlags;

    protected MemoryPropertyFlags RequiredProperties = requiredProperties;

    public VkContext Ctx { get; } = ctx;

    public VkDevice Device { get; } = device;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public abstract AllocationNode Allocate(
        MemoryRequirements requirements);

    public abstract void Deallocate(AllocationNode node);
    public abstract void Free();

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            Free();
            _disposedValue = true;
        }
    }

    ~VkAllocator()
    {
        Dispose(false);
    }
}

public interface IVkSingleTypeAllocator
{
    VkContext Ctx { get; }
    VkDevice Device { get; }

    bool TryAllocate(ulong size,
        ulong alignment,
        out AllocationNode node);

    void Deallocate(AllocationNode node);
}