using Silk.NET.Vulkan;

namespace FluidsVulkan.Vulkan;

public class VkSwapchain : IDisposable
{
    private readonly SwapchainKHR _swapchain;
    private readonly VkSwapchainContext _swapchainCtx;
    private bool _disposedValue;

    public VkSwapchain(VkContext ctx,
        SurfaceKHR surface,
        VkSwapchainContext swapchainCtx,
        uint[] familyIndices,
        uint imageCount,
        Format imageFormat,
        ColorSpaceKHR imageColorSpace,
        Extent2D imageExtent,
        PresentModeKHR presentMode,
        bool clipped = true,
        CompositeAlphaFlagsKHR compositeAlpha =
            CompositeAlphaFlagsKHR.OpaqueBitKhr,
        uint imageArrayLayers = 1u,
        ImageUsageFlags imageUsageFlags =
            ImageUsageFlags.ColorAttachmentBit,
        VkSwapchain oldSwapchain = null)
    {
        var swapchainCreateInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface,
            MinImageCount = imageCount,
            ImageFormat = imageFormat,
            ImageColorSpace = imageColorSpace,
            ImageExtent = imageExtent,
            ImageArrayLayers = imageArrayLayers,
            ImageUsage = imageUsageFlags,
            PreTransform =
                SurfaceTransformFlagsKHR.IdentityBitKhr,
            CompositeAlpha = compositeAlpha,
            PresentMode = presentMode,
            Clipped = clipped,
        };
        if (oldSwapchain != null)
            swapchainCreateInfo.OldSwapchain =
                oldSwapchain._swapchain;
        var hashSet = familyIndices.ToHashSet();
        unsafe
        {
            fixed (uint* pFamilyIndices = hashSet.ToArray())
            {
                if (hashSet.Count == 1)
                {
                    swapchainCreateInfo.ImageSharingMode =
                        SharingMode.Exclusive;
                    swapchainCreateInfo.QueueFamilyIndexCount = 0;
                    swapchainCreateInfo.PQueueFamilyIndices = null;
                }
                else
                {
                    swapchainCreateInfo.ImageSharingMode =
                        SharingMode.Concurrent;
                    swapchainCreateInfo.QueueFamilyIndexCount =
                        (uint)hashSet.Count;
                    swapchainCreateInfo.PQueueFamilyIndices =
                        pFamilyIndices;
                }

                swapchainCtx.CreateUnmanagedSwapchain(
                    in swapchainCreateInfo, out _swapchain);
            }
        }

        _swapchainCtx = swapchainCtx;
        Extent = imageExtent;
        Images = _swapchainCtx.GetSwapchainImages(_swapchain)
            .Select(
                z => new VkImage(z, imageFormat, ImageType.Type2D))
            .ToArray();
    }

    public Extent2D Extent { get; private set; }
    public VkImage[] Images { get; }

    internal SwapchainKHR Swapchain => _swapchain;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public Result AcquireNextImage(VkDevice device,
        VkSemaphore semaphore,
        out uint uIndex,
        VkFence fence = null)
    {
        var tmpFence = new Fence();
        uint tmp;
        Result result;
        if (fence != null) tmpFence = fence.InternalFence;

        unsafe
        {
            result = _swapchainCtx.Api.AcquireNextImage(device.Device,
                _swapchain, ulong.MaxValue, semaphore.Semaphore,
                tmpFence, &tmp);
        }

        uIndex = tmp;
        return result;
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        _swapchainCtx.DestroySwapchain(_swapchain);
        _disposedValue = true;
    }

    ~VkSwapchain()
    {
        Dispose(false);
    }
}