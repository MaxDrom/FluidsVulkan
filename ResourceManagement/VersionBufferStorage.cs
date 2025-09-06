using FluidsVulkan.Vulkan;
using FluidsVulkan.Vulkan.VkAllocatorSystem;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ResourceManagement;

public interface IVersionBufferStorage
{
    IVkBuffer GetWriteHandle();
    IVkBuffer GetReadHandle();

    ulong Size { get; }
}

public class VersionBufferStorage<T> : IVersionBufferStorage,
    IDisposable
    where T : unmanaged
{
    private uint _versions;
    private VkBuffer<T>[] _buffers;
    private uint _currentVersion;

    public VersionBufferStorage(
        int length,
        BufferUsageFlags usageFlags,
        SharingMode sharingMode,
        VkAllocator allocator,
        uint versions = 2)
    {
        _currentVersion = 0;
        _versions = versions;
        _buffers = new VkBuffer<T>[versions];
        for (int i = 0; i < versions; i++)
            _buffers[i] = new VkBuffer<T>(length, usageFlags,
                sharingMode, allocator);
    }

    public VersionBufferStorage(
        ulong size,
        BufferUsageFlags usageFlags,
        SharingMode sharingMode,
        VkAllocator allocator,
        uint versions = 2)
    {
        _currentVersion = 0;
        _versions = versions;
        _buffers = new VkBuffer<T>[versions];
        for (int i = 0; i < versions; i++)
            _buffers[i] = new VkBuffer<T>(size, usageFlags,
                sharingMode, allocator);
    }

    public IVkBuffer GetWriteHandle()
    {
        while (true)
        {
            var original = Volatile.Read(ref _currentVersion);
            var next = (original + 1) % _versions;

            var prev =
                Interlocked.CompareExchange(ref _currentVersion, next,
                    original);
            if (prev == original)
                return _buffers[next];
        }
    }

    public IVkBuffer GetReadHandle()
    {
        return _buffers[_currentVersion % _versions];
    }

    public ulong Size => _buffers[0].Size;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}