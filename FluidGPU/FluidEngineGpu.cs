using System.Runtime.InteropServices;
using Autofac.Features.AttributeFilters;
using FluidsVulkan.ComputeScheduling;
using FluidsVulkan.ImGui;
using FluidsVulkan.Vulkan;
using FluidsVulkan.Vulkan.VkAllocatorSystem;
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
    public float viscosityMult;
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
    private readonly VkAllocator _allocator;
    private readonly uint _boidsCount;
    private readonly VkTexture _bucketSizes;

    private readonly VkImageView _bucketSizesView;
    private readonly ComputeShader<uint> _countShader;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly uint _gridSize = 256;
    private readonly VkBuffer<Fluid> _newParticles;

    private readonly VkTexture _prefixSum;
    private readonly PrefixSumGPU _prefixSumGpu;
    private readonly VkImageView _prefixSumView;
    private readonly ComputeShader<uint> _replaceShader;
    private readonly VkAllocator _stagingAllocator;
    private VkBuffer<float> _densityBuffer;
    private ComputeShader<DensityPushConstant> _densityShader;
    private float _perceptionRadius;
    private ComputeShader<PredictPushConstant> _predictShader;
    private ComputeShader<UpdatePushConstant> _updateShader;


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
        _perceptionRadius = 1.0f / _gridSize;
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

        Buffer = new VkBuffer<Fluid>(initialData.Length,
            BufferUsageFlags.StorageBufferBit |
            BufferUsageFlags.TransferSrcBit |
            BufferUsageFlags.TransferDstBit,
            SharingMode.Exclusive, _allocator);

        _newParticles = new VkBuffer<Fluid>(initialData.Length,
            BufferUsageFlags.StorageBufferBit |
            BufferUsageFlags.TransferSrcBit |
            BufferUsageFlags.TransferDstBit,
            SharingMode.Exclusive, _allocator);


        var stagingBuffer = new VkBuffer<Fluid>(
            initialData.Length,
            BufferUsageFlags.TransferSrcBit,
            SharingMode.Exclusive,
            _stagingAllocator);

        using (var mapped = stagingBuffer.Map(0, initialData.Length))
        {
            for (var i = 0; i < initialData.Length; i++)
                mapped[i] = initialData[i];
        }


        ComputeScheduler.Instance.AddTask(new CopyBufferTask(
            stagingBuffer,
            Buffer,
            (uint)(Marshal.SizeOf<Fluid>() * initialData.Length)));
        

        InitTexture(_prefixSum);
        InitTexture(_bucketSizes);

        _countShader.SetBufferStorage(0, Buffer,
            AccessFlags.ShaderReadBit);
        _countShader.SetBufferStorage(1, _newParticles,
            AccessFlags.ShaderReadBit);
        _countShader.SetImageStorage(2, _bucketSizesView,
            AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);
        _countShader.SetImageStorage(3, _prefixSumView,
            AccessFlags.ShaderReadBit);
        _countShader.SetPushConstant(_boidsCount);

        _replaceShader.SetBufferStorage(0, Buffer,
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

    [SliderFloat("Perception Radius", 0, 1)]
    public float PerceptionRadius
    {
        get => _perceptionRadius * _gridSize;
        set => _perceptionRadius = value / _gridSize;
    }

    [SliderFloat("Target Density", 0, 50)]
    public float TargetDensity { get; set; }

    [SliderFloat("Density Mult", 0, 1000)]
    public float DensityMult { get; set; } = 1;

    [SliderFloat("Viscosity", 0, 500)]
    public float ViscosityMult { get; set; } = 300f;

    public VkBuffer<Fluid> Buffer { get; }


    public Task Update(double delta, double totalTime)
    {
        _predictShader.SetPushConstant(new PredictPushConstant
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

        _updateShader.SetPushConstant(new UpdatePushConstant
        {
            bufferLength = _boidsCount,
            delta = (float)delta,
            perceptionRadius = _perceptionRadius,
            densityMult = DensityMult,
            targetDensity = TargetDensity,
            viscosityMult = ViscosityMult,
        });
        _updateShader.Dispatch(
            (uint)Math.Ceiling(_boidsCount / 1024.0),
            1, 1);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _densityBuffer.Dispose();
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
        Buffer.Dispose();
        _newParticles.Dispose();
    }

    private void InitUpdatePipeline()
    {
        _updateShader = new ComputeShader<UpdatePushConstant>(_ctx,
            _device, "shader_objects/force.comp.spv");

        _updateShader.SetBufferStorage(0, _newParticles,
            AccessFlags.ShaderReadBit);
        _updateShader.SetBufferStorage(2, Buffer,
            AccessFlags.ShaderWriteBit);
        _updateShader.SetImageStorage(3, _prefixSumView,
            AccessFlags.ShaderReadBit);
    }

    private void InitPredictPipeline()
    {
        _predictShader = new ComputeShader<PredictPushConstant>(_ctx,
            _device,
            "shader_objects/predict.comp.spv");

        _predictShader.SetBufferStorage(0, Buffer,
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
        _densityShader.SetPushConstant(new DensityPushConstant
        {
            bufferLength = _boidsCount,
            perceptionRadius = _perceptionRadius,
        });
    }

    private void InitTexture(VkTexture texture)
    {
        var stagingBuffer = new VkBuffer<uint>(
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

        ComputeScheduler.Instance.AddTask(
            new CopyBufferToImageTask(stagingBuffer, texture.Image,
                [region]));
    }
}