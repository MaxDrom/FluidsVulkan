using System.Runtime.InteropServices;
using Autofac.Features.AttributeFilters;
using FluidsVulkan.ImGui;
using FluidsVulkan.VkAllocatorSystem;
using ImGuiNET;
using Silk.NET.Vulkan;

namespace FluidsVulkan.FluidGPU;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct UpdatePushConstant
{
    public uint bufferLength;
    public float perceptionRadius;
    public float delta;
    public float targetDensity;
    public float densityMult;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct DensityPushConstant
{
    public uint bufferLength;
    public float perceptionRadius;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct PredictPushConstant
{
    public uint bufferLength;
    public float delta;
}

public class FluidEngineGpu : IParticleSystem, IParametrized
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly VkAllocator _allocator;
    private readonly VkAllocator _stagingAllocator;
    private readonly VkTexture _bucketSizes;
    private readonly VkTexture _prefixSum;

    private readonly VkImageView _bucketSizesView;
    private readonly VkImageView _prefixSumView;
    private PrefixSumGPU _prefixSumGpu;

    private ComputeShader<uint> _countShader;
    private ComputeShader<uint> _replaceShader;
    private ComputeShader<DensityPushConstant> _densityShader;
    private ComputeShader<UpdatePushConstant> _updateShader;
    private ComputeShader<PredictPushConstant> _predictShader;

    private VkBuffer<Fluid> _oldParticles;
    private VkBuffer<Fluid> _newParticles;
    private VkFence _fence;
    private uint _boidsCount;
    private readonly VkCommandBuffer _cmdBufferCopy;
    private readonly VkCommandPool _copyPool;
    private VkBuffer<float> _densityBuffer;
    private uint _gridSize = 128;
    private float _targetDensity = 10;
    private float _densityMult = 10;
    private float _perceptionRadius;
    
    [SliderFloat("Perception Radius", 0, 1 )]
    public float PerceptionRadius
    {
        get => _perceptionRadius*_gridSize;
        set => _perceptionRadius = value/(float)_gridSize;
    }

    [SliderFloat("Target Density", 0, 50 )]
    public float TargetDensity
    {
        get => _targetDensity;
        set => _targetDensity = value;
    }
    
    [SliderFloat("Density Mult", 0, 1000 )]
    public float DensityMult
    {
        get => _densityMult;
        set => _densityMult = value;
    }
    public VkBuffer<Fluid> Buffer => _oldParticles;


    public FluidEngineGpu(VkContext ctx,
        VkDevice device,
        [MetadataFilter("Type", "DeviceLocal")]
        VkAllocator allocator,
        [MetadataFilter("Type", "HostVisible")]
        VkAllocator stagingAllocator,
        Fluid[] initialData)
    {
        _boidsCount = (uint)initialData.Length;
        _ctx = ctx;
        _device = device;
        _perceptionRadius = 1.0f/_gridSize;
        _allocator = allocator;
        _stagingAllocator = stagingAllocator;
        _bucketSizes = new VkTexture(ImageType.Type2D,
            new Extent3D(_gridSize, _gridSize, 1), 1, 1,
            Format.R32Uint,
            ImageTiling.Optimal, ImageLayout.Undefined,
            ImageUsageFlags.StorageBit |
            ImageUsageFlags.TransferDstBit,
            SampleCountFlags.Count1Bit,
            SharingMode.Exclusive, _allocator);

        _prefixSum = new VkTexture(ImageType.Type2D,
            new Extent3D(_gridSize, _gridSize, 1), 1, 1,
            Format.R32Uint,
            ImageTiling.Optimal, ImageLayout.Undefined,
            ImageUsageFlags.StorageBit |
            ImageUsageFlags.TransferDstBit,
            SampleCountFlags.Count1Bit,
            SharingMode.Exclusive, _allocator);

        _bucketSizesView = new VkImageView(_ctx, _device,
            _bucketSizes.Image,
            new ComponentMapping(ComponentSwizzle.Identity,
                ComponentSwizzle.Identity, ComponentSwizzle.Identity,
                ComponentSwizzle.Identity),
            new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1,
                0, 1)
        );

        _prefixSumView = new VkImageView(_ctx, _device,
            _prefixSum.Image,
            new ComponentMapping(ComponentSwizzle.Identity,
                ComponentSwizzle.Identity, ComponentSwizzle.Identity,
                ComponentSwizzle.Identity),
            new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1,
                0, 1)
        );


        _prefixSumGpu = new PrefixSumGPU(_ctx, _device);

        _countShader = new ComputeShader<uint>(ctx, device,
            "shader_objects/count.comp.spv");

        _replaceShader = new ComputeShader<uint>(ctx, device,
            "shader_objects/replace.comp.spv");

        _oldParticles = new VkBuffer<Fluid>(initialData.Length,
            BufferUsageFlags.StorageBufferBit |
            BufferUsageFlags.TransferSrcBit |
            BufferUsageFlags.TransferDstBit |
            BufferUsageFlags.VertexBufferBit,
            SharingMode.Exclusive, _allocator);

        _newParticles = new VkBuffer<Fluid>(initialData.Length,
            BufferUsageFlags.StorageBufferBit |
            BufferUsageFlags.TransferSrcBit |
            BufferUsageFlags.TransferDstBit,
            SharingMode.Exclusive, _allocator);


        using var stagingBuffer = new VkBuffer<Fluid>(
            initialData.Length,
            BufferUsageFlags.TransferSrcBit,
            SharingMode.Exclusive,
            _stagingAllocator);

        using (var mapped = stagingBuffer.Map(0, initialData.Length))
        {
            for (var i = 0; i < initialData.Length; i++)
                mapped[i] = initialData[i];
        }
        
        _copyPool = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.FamilyIndex);

        _cmdBufferCopy =
            _copyPool.AllocateBuffers(CommandBufferLevel.Primary,
                1)[0];

        using (var recording =
               _cmdBufferCopy.Begin(CommandBufferUsageFlags
                   .OneTimeSubmitBit))
        {
            recording.CopyBuffer(stagingBuffer, Buffer, 0, 0,
                (uint)(Marshal.SizeOf<Fluid>() * initialData.Length));
        }

        _cmdBufferCopy.Submit(_device.TransferQueue,
            VkFence.NullHandle,
            [], []);

        _ctx.Api.QueueWaitIdle(_device.TransferQueue);

        _fence = new VkFence(_ctx, _device);
        _fence.Reset();

        InitTexture(_prefixSum);
        InitTexture(_bucketSizes);

        _countShader.SetBufferStorage(0, _oldParticles,
            AccessFlags.ShaderReadBit);
        _countShader.SetBufferStorage(1, _newParticles,
            AccessFlags.ShaderReadBit);
        _countShader.SetImageStorage(2, _bucketSizesView,
            AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);
        _countShader.SetImageStorage(3, _prefixSumView,
            AccessFlags.ShaderReadBit);
        _countShader.SetPushConstant(_boidsCount);

        _replaceShader.SetBufferStorage(0, _oldParticles,
            AccessFlags.ShaderReadBit);
        _replaceShader.SetBufferStorage(1, _newParticles,
            AccessFlags.ShaderWriteBit);
        _replaceShader.SetImageStorage(2, _bucketSizesView,
            AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);
        _replaceShader.SetImageStorage(3, _prefixSumView,
            AccessFlags.ShaderReadBit);
        _replaceShader.SetPushConstant(_boidsCount);

        InitDensityPipeline();
        InitUpdatePipeline();
        InitPredictPipeline();
    }

    private void InitUpdatePipeline()
    {
        _updateShader = new ComputeShader<UpdatePushConstant>(_ctx,
            _device, "shader_objects/force.comp.spv");

        _updateShader.SetBufferStorage(0, _newParticles,
            AccessFlags.ShaderReadBit);
        _updateShader.SetBufferStorage(2, _oldParticles,
            AccessFlags.ShaderWriteBit);
        _updateShader.SetImageStorage(3, _prefixSumView,
            AccessFlags.ShaderReadBit);
    }

    private void InitPredictPipeline()
    {
        _predictShader = new ComputeShader<PredictPushConstant>(_ctx,
            _device,
            "shader_objects/predict.comp.spv");

        _predictShader.SetBufferStorage(0, _oldParticles,
            AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);
    }

    private void InitDensityPipeline()
    {
        _densityBuffer = new VkBuffer<float>((int)_boidsCount,
            BufferUsageFlags.StorageBufferBit,
            SharingMode.Exclusive, _allocator);

        _densityShader = new ComputeShader<DensityPushConstant>(_ctx,
            _device,
            "shader_objects/density.comp.spv");

        _densityShader.SetBufferStorage(0, _newParticles,
            AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);
        _densityShader.SetImageStorage(2, _prefixSumView,
            AccessFlags.ShaderReadBit);
        _densityShader.SetPushConstant(new DensityPushConstant()
        {
            bufferLength = _boidsCount,
            perceptionRadius = _perceptionRadius
        });
    }


    public Task Update(double delta, double totalTime)
    {

            _predictShader.SetPushConstant(new PredictPushConstant()
            {
                bufferLength = _boidsCount,
                delta = (float)delta,
            });
            _predictShader.Dispatch(
                (uint)Math.Ceiling(_boidsCount / 1024.0), 1, 1);


            _countShader.Dispatch(
                (uint)Math.Ceiling(_boidsCount / 1024.0), 1, 1);


            _prefixSumGpu.RecordBuffer(_bucketSizesView,
                _prefixSumView, ((int)_gridSize, (int)_gridSize));


            _replaceShader.Dispatch(
                (uint)Math.Ceiling(_boidsCount / 1024.0),
                1, 1);

            _densityShader.Dispatch(
                (uint)Math.Ceiling(_boidsCount / 1024.0), 1, 1);

            _updateShader.SetPushConstant(new UpdatePushConstant()
            {
                bufferLength = _boidsCount,
                delta = (float)delta,
                perceptionRadius = _perceptionRadius,
                densityMult = _densityMult,
                targetDensity = _targetDensity
            });
            _updateShader.Dispatch(
                (uint)Math.Ceiling(_boidsCount / 1024.0),
                1, 1);
            return Task.CompletedTask;
    }

    private void InitTexture(VkTexture texture)
    {
        _cmdBufferCopy.Reset(CommandBufferResetFlags.None);
        using var stagingBuffer = new VkBuffer<uint>(
            (int)(texture.Extent.Width * texture.Extent.Height),
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            _stagingAllocator);
        using (var mapped =
               stagingBuffer.Map(0,
                   (int)(texture.Extent.Width *
                         texture.Extent.Height)))
        {
            for (var i = 0;
                 i < texture.Extent.Width * texture.Extent.Height;
                 i++)
                mapped[i] = 0;
        }

        using (var recording =
               _cmdBufferCopy.Begin(CommandBufferUsageFlags.None))
        {
            var region = new BufferImageCopy
            {
                BufferOffset = 0,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource =
                    new ImageSubresourceLayers
                    {
                        AspectMask =
                            ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = texture.Extent,
            };

            ImageMemoryBarrier barrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                DstAccessMask = AccessFlags.TransferWriteBit,
                SrcAccessMask = AccessFlags.None,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                Image = texture.Image.Image,
                SubresourceRange =
                    new ImageSubresourceRange(
                        ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            };

            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.TransferBit, 0,
                imageMemoryBarriers: [barrier]);

            _ctx.Api.CmdCopyBufferToImage(_cmdBufferCopy.Buffer,
                stagingBuffer.Buffer,
                texture.Image.Image,
                ImageLayout.TransferDstOptimal, 1, in region);


            ImageMemoryBarrier barrier1 = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                DstAccessMask = AccessFlags.None,
                SrcAccessMask = AccessFlags.TransferWriteBit,
                OldLayout = ImageLayout.TransferDstOptimal,
                NewLayout = ImageLayout.General,
                Image = texture.Image.Image,
                SubresourceRange =
                    new ImageSubresourceRange(
                        ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            };

            recording.PipelineBarrier(PipelineStageFlags.TransferBit,
                PipelineStageFlags.BottomOfPipeBit, 0,
                imageMemoryBarriers: [barrier1]);
        }

        _cmdBufferCopy.Submit(_device.TransferQueue,
            VkFence.NullHandle, [], []);
        _ctx.Api.QueueWaitIdle(_device.TransferQueue);
        _cmdBufferCopy.Reset(CommandBufferResetFlags.None);
    }

    public void Dispose()
    {
        _densityBuffer.Dispose();
        _copyPool.Dispose();
        _fence.Dispose();
        _countShader.Dispose();
        _predictShader.Dispose();
        _replaceShader.Dispose();
        _densityShader.Dispose();
        _updateShader.Dispose();
        _prefixSumGpu.Dispose();
        _prefixSumView.Dispose();
        _prefixSum.Dispose();
        _bucketSizesView.Dispose();
        _bucketSizes.Dispose();
        _oldParticles.Dispose();
        _newParticles.Dispose();
    }
}