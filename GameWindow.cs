using Autofac.Features.AttributeFilters;
using FluidsVulkan.FluidGPU;
using FluidsVulkan.VkAllocatorSystem;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace FluidsVulkan;

public sealed class GameWindow : IDisposable
{
    private readonly VkAllocator _allocator;
    private readonly VkCommandBuffer[] _buffers;

    private readonly ColorSpaceKHR _colorSpace;

    private readonly VkCommandPool _commandPool;

    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly VkFence[] _fences;

    private readonly Format _format;

    private const int FramesInFlight = 2;
    private readonly VkSemaphore[] _imageAvailableSemaphores;


    private readonly VkSemaphore[] _renderFinishedSemaphores;
    private readonly VkAllocator _stagingAllocator;
    private readonly VkSwapchainContext _swapchainCtx;

    private bool _disposedValue;
    private int _fps;
    private int _frameIndex;
    private VkSwapchain _swapchain;
    private VkTexture _textureBuffer;
    private VkImageView _textureBufferView;
    private double _totalFrameTime;
    private List<VkImageView> _views;
    private IParticleSystem _particleSystem;
    private WindowOptions _windowOptions;
    private readonly IWindow _window;
    private readonly VkCommandPool _commandPoolTransfer;
    private FluidController _controller;
    public GameWindow(
        VkContext ctx,
        VkDevice device,
        DisplayFormat displayFormat,
        [MetadataFilter("Type", "DeviceLocal")]
        VkAllocator allocator,
        [MetadataFilter("Type", "HostVisible")]
        VkAllocator stagingAllocator,
        IParticleSystem particleSystem,
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
        _particleSystem = particleSystem;
        _windowOptions = displayFormat.WindowOptions;
        _window = window;
     
        _swapchainCtx = new VkSwapchainContext(_ctx, _device);
        _commandPool = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.FamilyIndex);

        _commandPoolTransfer = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.FamilyIndex);


        CreateSwapchain();
        CreateRenderTarget();
        CreateViews();

        _buffers =
            _commandPool.AllocateBuffers(CommandBufferLevel.Primary,
                FramesInFlight);

        _view = new FluidView(ctx, device, _allocator,
            _stagingAllocator, _textureBufferView,
            _textureBuffer.Extent);

        _view.Update(_particleSystem.Buffer);

        //for (var i = 0; i < _views.Count; i++)
          //  RecordBuffer(_buffers[i], i);

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
        
        
        _controller =
            new FluidController(_eventHandler, _view, _particleSystem);

        _frameIndex = 0;
        _totalFrameTime = 0d;
        _fps = 0;
        _window.Render += OnRender;
        _window.Closing +=
            () => _ctx.Api.DeviceWaitIdle(_device.Device);
        _window.Resize += OnResize;
        _window.Update += x => OnUpdate(x).GetAwaiter().GetResult();
        
    }

    public void Dispose()
    {
        Dispose(true);
    }

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

            _commandPool.Dispose();
            _commandPoolTransfer.Dispose();
            _view.Dispose();
            _textureBufferView.Dispose();
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
                    _device.FamilyIndex
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
            new Extent3D(_swapchain.Extent.Width,
                _swapchain.Extent.Height, 1), 1, 1, _format,
            ImageTiling.Optimal, ImageLayout.Undefined,
            ImageUsageFlags.ColorAttachmentBit |
            ImageUsageFlags.TransferSrcBit |
            ImageUsageFlags.SampledBit | ImageUsageFlags.StorageBit |
            ImageUsageFlags.TransferDstBit,
            SampleCountFlags.Count1Bit, SharingMode.Exclusive,
            _allocator);

        using var stagingBuffer = new VkBuffer<byte>(_textureBuffer.Size,
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            _stagingAllocator);
        using (var mapped =
               stagingBuffer.Map(0, _textureBuffer.Size))
        {
            for (var i = 0u; i < mapped.Length; i++) mapped[i] = 0;
        }

        var copyRegion = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource =
                new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = _textureBuffer.Extent,
        };
        var copyBuffer = _commandPoolTransfer
            .AllocateBuffers(CommandBufferLevel.Primary, 1).First();
        using (var recording =
               copyBuffer.Begin(CommandBufferUsageFlags
                   .OneTimeSubmitBit))
        {
            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.General,
                Image = _textureBuffer.Image.Image,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                SrcAccessMask = 0,
                DstAccessMask = AccessFlags.TransferWriteBit,
            };

            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.TransferBit, DependencyFlags.None,
                imageMemoryBarriers: [barrier]);

            _ctx.Api.CmdCopyBufferToImage(copyBuffer.Buffer,
                stagingBuffer.Buffer, _textureBuffer.Image.Image,
                ImageLayout.General,
                new ReadOnlySpan<BufferImageCopy>(ref copyRegion));
        }

        copyBuffer.Submit(_device.TransferQueue, VkFence.NullHandle,
            [], []);
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

        _textureBufferView = new VkImageView(_ctx, _device,
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

    private FluidView _view;

    private bool _firstRun = true;
    private uint _imageIndex;

    private double _totalTime;
    private readonly EventHandler _eventHandler;

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

        ImageMemoryBarrier bb = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.General,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.MemoryReadBit,
            Image = _textureBuffer.Image.Image,
            SubresourceRange = subresourceRange,
        };

        recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            DependencyFlags.None, imageMemoryBarriers: [bb]);


        _view.RecordDraw(scissor, viewport,
            recording);

        var region = new ImageBlit()
        {
            SrcSubresource =
                new ImageSubresourceLayers(
                    ImageAspectFlags.ColorBit, 0, 0, 1),
            DstSubresource =
                new ImageSubresourceLayers(
                    ImageAspectFlags.ColorBit, 0, 0, 1),
        };
        region.SrcOffsets[0] = new(0, 0, 0);
        region.DstOffsets[0] = new(0, 0, 0);
        region.SrcOffsets[1] = new(
            (int)_textureBuffer.Extent.Width,
            (int)_textureBuffer.Extent.Height, 1);
        region.DstOffsets[1] = new((int)_swapchain.Extent.Width,
            (int)_swapchain.Extent.Height, 1);

        ImageMemoryBarrier[] barriers =
        [
            new()
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferSrcOptimal,
                SrcAccessMask =
                    AccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = AccessFlags.TransferReadBit,
                Image = _textureBuffer.Image.Image,
                SubresourceRange = subresourceRange,
            },

            new()
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                SrcAccessMask = AccessFlags.None,
                DstAccessMask = AccessFlags.TransferWriteBit,
                Image = _swapchain.Images[imageIndex].Image,
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

        ImageMemoryBarrier barrier2 = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.TransferDstOptimal,
            NewLayout = ImageLayout.PresentSrcKhr,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.None,
            Image = _swapchain.Images[imageIndex].Image,
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
        foreach (var view in _views)
        {
            view.Dispose();
        }
        CreateSwapchain();
        
        oldSwapchain.Dispose();
        
        CreateViews();
    }

    static double _fixedUpdateInterval = 1/120.0;
    static double _timeFromFixedUpdate = _fixedUpdateInterval;
    private async Task OnUpdate(double frameTime)
    {
        _eventHandler.Update();
        if (_firstRun)
        {
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

        _timeFromFixedUpdate += frameTime;
        while (_timeFromFixedUpdate >= _fixedUpdateInterval)
        {
            await _particleSystem.Update(_fixedUpdateInterval, _totalTime);
            _timeFromFixedUpdate -= _fixedUpdateInterval;
        }
        
        
        
        await _view.Update(_particleSystem.Buffer);
        _totalTime += frameTime;
    }

    private void OnRender(double frameTime)
    {
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