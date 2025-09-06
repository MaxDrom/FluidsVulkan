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
public class VersionBufferStorage<T> : IVersionBufferStorage, IDisposable
    where T : unmanaged
{
    private int _versions;
    private VkBuffer<T>[] _buffers;
    private int _currentVersion;
    public VersionBufferStorage(
        int length,
        BufferUsageFlags usageFlags,
        SharingMode sharingMode,
        VkAllocator allocator,
        int versions = 2)
    {
        _currentVersion = 0;
        _versions = versions;
        _buffers = new VkBuffer<T>[versions];
        for (int i = 0; i < versions; i++)
            _buffers[i] = new VkBuffer<T>(length, usageFlags, sharingMode, allocator);
    }
    
    public VersionBufferStorage(
        ulong size,
        BufferUsageFlags usageFlags,
        SharingMode sharingMode,
        VkAllocator allocator,
        int versions = 2)
    {
        _currentVersion = 0;
        _versions = versions;
        _buffers = new VkBuffer<T>[versions];
        for (int i = 0; i < versions; i++)
            _buffers[i] = new VkBuffer<T>(size, usageFlags, sharingMode, allocator);
    }
    
    private readonly Lock _syncRoot = new();
    public IVkBuffer GetWriteHandle()
    {
        lock (_syncRoot)
        {
           _currentVersion ++;
           _currentVersion = _currentVersion % _versions;
        }

        return _buffers[_currentVersion];
    }

    public IVkBuffer GetReadHandle()
    {
        return _buffers[_currentVersion];
    }

    public ulong Size => _buffers[0].Size;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        foreach (var buffer in _buffers )
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