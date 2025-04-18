using Autofac.Features.AttributeFilters;
using FluidsVulkan.ComputeScheduling;
using FluidsVulkan.Vulkan;
using FluidsVulkan.Vulkan.VkAllocatorSystem;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace FluidsVulkan;

public sealed class GameWindow : IDisposable
{
    private const int FramesInFlight = 2;
    private readonly VkAllocator _allocator;
    private readonly VkCommandBuffer[] _buffers;

    private readonly ColorSpaceKHR _colorSpace;

    private readonly VkCommandPool _commandPool;

    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly EventHandler _eventHandler;
    private readonly VkFence[] _fences;

    private readonly Format _format;
    private readonly VkSemaphore[] _imageAvailableSemaphores;


    private readonly VkSemaphore[] _renderFinishedSemaphores;
    private readonly VkAllocator _stagingAllocator;
    private readonly VkSwapchainContext _swapchainCtx;
    private readonly IWindow _window;

    private VkCommandBuffer _computeBuffer;
    private VkFence _computeFence;

    private bool _disposedValue;

    private bool _firstRun = true;

    private bool _firstRunRender = true;
    private int _fps;
    private int _frameIndex;
    private uint _imageIndex;
    private VkSwapchain _swapchain;
    private VkTexture _textureBuffer;
    private double _totalFrameTime;

    private double _totalTime;
    private List<VkImageView> _views;
    private WindowOptions _windowOptions;

    public GameWindow(
        VkContext ctx,
        VkDevice device,
        DisplayFormat displayFormat,
        [MetadataFilter("Type", "DeviceLocal")]
        VkAllocator allocator,
        [MetadataFilter("Type", "HostVisible")]
        VkAllocator stagingAllocator,
        IWindow window,
        EventHandler eventHandler)
    {
        _eventHandler = eventHandler;
        _ctx = ctx;
        _device = device;
        _format = displayFormat.Format;
        _colorSpace = displayFormat.ColorSpace;
        _allocator = allocator;
        _stagingAllocator = stagingAllocator;
        _windowOptions = displayFormat.WindowOptions;
        _window = window;
        _swapchainCtx = new VkSwapchainContext(_ctx, _device);
        _commandPool = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.FamilyIndex);

        CreateSwapchain();
        CreateRenderTarget();
        CreateViews();

        _buffers =
            _commandPool.AllocateBuffers(CommandBufferLevel.Primary,
                FramesInFlight);

        _fences = new VkFence[FramesInFlight];
        _imageAvailableSemaphores = new VkSemaphore[FramesInFlight];
        _renderFinishedSemaphores = new VkSemaphore[FramesInFlight];

        for (var i = 0; i < FramesInFlight; i++)
        {
            _fences[i] = new VkFence(_ctx, _device);
            _imageAvailableSemaphores[i] =
                new VkSemaphore(_ctx, _device)
                {
                    Flag = PipelineStageFlags
                        .ColorAttachmentOutputBit,
                };
            _renderFinishedSemaphores[i] =
                new VkSemaphore(_ctx, _device);
        }


        _frameIndex = 0;
        _totalFrameTime = 0d;
        _fps = 0;
        _window.Render += Render;
        _window.Closing +=
            () => _ctx.Api.DeviceWaitIdle(_device.Device);
        _window.Resize += OnResize;
        _window.Update += x => Update(x).GetAwaiter().GetResult();
        _window.Update += x => OnUpdate?.Invoke(x, _totalTime);
    }

    public VkImageView RenderTarget { get; private set; }

    public Extent2D WindowSize => new(
        _textureBuffer.Extent.Width, _textureBuffer.Extent.Height);

    public void Dispose()
    {
        Dispose(true);
    }


    public event Func<double, double, Task> OnUpdateAsync;
    public event Action<double, double> OnUpdate;
    public event Action<VkCommandRecordingScope, Rect2D> OnRender;

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            foreach (var view in _views) view.Dispose();
            foreach (var fence in _fences) fence.Dispose();
            foreach (var sem in _imageAvailableSemaphores)
                sem.Dispose();
            foreach (var sem in _renderFinishedSemaphores)
                sem.Dispose();

            _computeFence.Dispose();
            _commandPool.Dispose();
            RenderTarget.Dispose();
            _textureBuffer.Dispose();
            _swapchain.Dispose();
            _swapchainCtx.Dispose();
        }

        _disposedValue = true;
    }

    private void CleanUpSwapchain()
    {
        foreach (var image in _views) image.Dispose();
        _swapchain.Dispose();
    }

    private void CreateSwapchain()
    {
        unsafe
        {
            uint n;
            _ctx.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(
                _device.PhysicalDevice, _ctx.Surface, &n, null);

            var presentModes = stackalloc PresentModeKHR[(int)n];
            _ctx.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(
                _device.PhysicalDevice, _ctx.Surface, &n,
                presentModes);
            var presentMode = presentModes[0];
            var score = 0;
            Dictionary<PresentModeKHR, int> desired = new()
            {
                [PresentModeKHR.MailboxKhr] = 10,
                [PresentModeKHR.ImmediateKhr] = 5,
                [PresentModeKHR.FifoKhr] = 1,
            };
            for (var i = 0; i < n; i++)
                if (desired.TryGetValue(presentModes[i],
                        out var ss) && ss > score)
                {
                    presentMode = presentModes[i];
                    score = ss;
                }

            _ctx.SurfaceApi.GetPhysicalDeviceSurfaceCapabilities(
                _device.PhysicalDevice, _ctx.Surface,
                out var capabilities);
            var oldSwapchain = _swapchain;
            _swapchain = new VkSwapchain(_ctx, _ctx.Surface,
                _swapchainCtx, [
                    _device.FamilyIndex,
                ], capabilities.MinImageCount + 1, _format,
                _colorSpace,
                new Extent2D((uint)_window.FramebufferSize.X,
                    (uint)_window.FramebufferSize.Y), presentMode,
                imageUsageFlags: ImageUsageFlags.TransferDstBit |
                                 ImageUsageFlags.ColorAttachmentBit,
                oldSwapchain: oldSwapchain);
        }
    }

    private void CreateRenderTarget()
    {
        _textureBuffer = new VkTexture(ImageType.Type2D,
            new Extent3D(Math.Max(_swapchain.Extent.Width, 1920),
                Math.Max(_swapchain.Extent.Height, 1080), 1), 1, 1,
            _format,
            ImageTiling.Optimal, ImageLayout.Undefined,
            ImageUsageFlags.ColorAttachmentBit |
            ImageUsageFlags.TransferSrcBit |
            ImageUsageFlags.SampledBit | ImageUsageFlags.StorageBit |
            ImageUsageFlags.TransferDstBit,
            SampleCountFlags.Count1Bit, SharingMode.Exclusive,
            _allocator);

        _ctx.Api.QueueWaitIdle(_device.TransferQueue);

        var mapping = new ComponentMapping
        {
            A = ComponentSwizzle.Identity,
            B = ComponentSwizzle.Identity,
            R = ComponentSwizzle.Identity,
            G = ComponentSwizzle.Identity,
        };

        var subresourceRange = new ImageSubresourceRange
        {
            AspectMask = ImageAspectFlags.ColorBit,
            BaseArrayLayer = 0,
            BaseMipLevel = 0,
            LayerCount = 1,
            LevelCount = 1,
        };

        RenderTarget = new VkImageView(_ctx, _device,
            _textureBuffer.Image, mapping, subresourceRange);
    }

    private void CreateViews()
    {
        _views = [];
        var mapping = new ComponentMapping
        {
            A = ComponentSwizzle.Identity,
            B = ComponentSwizzle.Identity,
            R = ComponentSwizzle.Identity,
            G = ComponentSwizzle.Identity,
        };

        var subresourceRange = new ImageSubresourceRange
        {
            AspectMask = ImageAspectFlags.ColorBit,
            BaseArrayLayer = 0,
            BaseMipLevel = 0,
            LayerCount = 1,
            LevelCount = 1,
        };
        foreach (var image in _swapchain.Images)
            _views.Add(new VkImageView(_ctx, _device, image, mapping,
                subresourceRange));
    }

    public void Run()
    {
        _window.Run();
    }

    private void RecordBuffer(VkCommandBuffer buffer, int imageIndex)
    {
        Viewport viewport = new()
        {
            X = 0.0f,
            Y = 0.0f,
            Width = _textureBuffer.Extent.Width,
            Height = _textureBuffer.Extent.Height,
        };
        Rect2D scissor = new(new Offset2D(0, 0),
            new Extent2D(_textureBuffer.Extent.Width,
                _textureBuffer.Extent.Height));

        using var recording =
            buffer.Begin(CommandBufferUsageFlags.None);
        var subresourceRange =
            new ImageSubresourceRange(ImageAspectFlags.ColorBit,
                0, 1, 0, 1);

        VkImageMemoryBarrier bb = new()
        {
            NewLayout = ImageLayout.General,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.MemoryReadBit,
            Image = _textureBuffer.Image,
            SubresourceRange = subresourceRange,
        };

        recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            DependencyFlags.None, imageMemoryBarriers: [bb]);


        OnRender?.Invoke(recording, scissor);

        var region = new ImageBlit
        {
            SrcSubresource =
                new ImageSubresourceLayers(
                    ImageAspectFlags.ColorBit, 0, 0, 1),
            DstSubresource =
                new ImageSubresourceLayers(
                    ImageAspectFlags.ColorBit, 0, 0, 1),
        };
        region.SrcOffsets[0] = new Offset3D(0, 0, 0);
        region.DstOffsets[0] = new Offset3D(0, 0, 0);
        region.SrcOffsets[1] = new Offset3D(
            (int)_textureBuffer.Extent.Width,
            (int)_textureBuffer.Extent.Height, 1);
        region.DstOffsets[1] = new Offset3D(
            (int)_swapchain.Extent.Width,
            (int)_swapchain.Extent.Height, 1);

        VkImageMemoryBarrier[] barriers =
        [
            new()
            {
                NewLayout = ImageLayout.TransferSrcOptimal,
                SrcAccessMask =
                    AccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = AccessFlags.TransferReadBit,
                Image = _textureBuffer.Image,
                SubresourceRange = subresourceRange,
            },

            new()
            {
                NewLayout = ImageLayout.TransferDstOptimal,
                SrcAccessMask = AccessFlags.None,
                DstAccessMask = AccessFlags.TransferWriteBit,
                Image = _swapchain.Images[imageIndex],
                SubresourceRange = subresourceRange,
            },
        ];

        recording.PipelineBarrier(
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.TransferBit, DependencyFlags.None,
            imageMemoryBarriers: barriers);

        _ctx.Api.CmdBlitImage(buffer.Buffer,
            _textureBuffer.Image.Image,
            ImageLayout.TransferSrcOptimal,
            _swapchain.Images[imageIndex].Image,
            ImageLayout.TransferDstOptimal, [region],
            Filter.Linear);


        VkImageMemoryBarrier barrier2 = new()
        {
            NewLayout = ImageLayout.PresentSrcKhr,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.None,
            Image = _swapchain.Images[imageIndex],
            SubresourceRange = subresourceRange,
        };

        recording.PipelineBarrier(PipelineStageFlags.TransferBit,
            PipelineStageFlags.BottomOfPipeBit,
            DependencyFlags.None,
            imageMemoryBarriers: [barrier2]);
    }

    private void OnResize(Vector2D<int> x)
    {
        //_ctx.Api.DeviceWaitIdle(_device.Device);
        var oldSwapchain = _swapchain;
        foreach (var view in _views) view.Dispose();

        CreateSwapchain();

        oldSwapchain.Dispose();

        CreateViews();
    }

    private async Task Update(double frameTime)
    {
        _eventHandler.Update();
        if (_firstRun)
        {
            _computeFence = new VkFence(_ctx, _device);
            _computeBuffer =
                _commandPool.AllocateBuffers(
                    CommandBufferLevel.Primary, 1)[0];
            _firstRun = false;
            _totalTime = 0;
            return;
        }

        _totalFrameTime += frameTime;
        _fps++;
        if (_totalFrameTime >= 1)
        {
            _window.Title = $"FPS: {_fps / _totalFrameTime}";
            _totalFrameTime = 0;
            _fps = 0;
        }

        if (OnUpdateAsync != null)
            await OnUpdateAsync!.Invoke(frameTime, _totalTime);
        _computeBuffer.Reset(CommandBufferResetFlags
            .None);
        using (var recording =
               _computeBuffer.Begin(CommandBufferUsageFlags
                   .SimultaneousUseBit))
        {
            await ComputeScheduler.Instance.RecordAll(recording);
        }

        _computeFence.Reset();
        _computeBuffer.Submit(_device.ComputeQueue, _computeFence, [],
            []);
        await _computeFence.WaitFor();
        _totalTime += frameTime;
    }

    private void Render(double frameTime)
    {
        if (_firstRunRender)
        {
            _firstRunRender = false;
            return;
        }

        _fences[_frameIndex].WaitFor().GetAwaiter().GetResult();
        if (_swapchain.AcquireNextImage(_device,
                _imageAvailableSemaphores[_frameIndex],
                out _imageIndex) == Result.ErrorOutOfDateKhr)
            return;

        _fences[_frameIndex].Reset();
        _buffers[_frameIndex].Reset(CommandBufferResetFlags.None);
        RecordBuffer(_buffers[_frameIndex], (int)_imageIndex);
        _buffers[_frameIndex].Submit(_device.GraphicsQueue,
            _fences[_frameIndex],
            [_imageAvailableSemaphores[_frameIndex]],
            [_renderFinishedSemaphores[_frameIndex]]);
        _swapchainCtx.QueuePresent(_device.PresentQueue,
            [_imageIndex],
            [_swapchain], [_renderFinishedSemaphores[_frameIndex]]);
        _frameIndex = ++_frameIndex % FramesInFlight;
    }
}