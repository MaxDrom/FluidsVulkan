using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace FluidsVulkan;

public class ComputeShader<T> : IDisposable
    where T : unmanaged
{
    private VkContext _ctx;
    private VkDevice _device;
    private VkDescriptorPool _descriptorPool;
    private VkSetLayout _layout;
    private DescriptorSet _descriptorSet;
    private VkComputePipeline _computePipeline;
    private T _pushConstant;

    private Dictionary<int, (IVkBuffer, AccessFlags)>
        _bufferBindings =
            [];

    private Dictionary<int, (VkImageView, AccessFlags)>
        _imageBindings =
            [];

    public ComputeShader(VkContext ctx,
        VkDevice device,
        string spvPath,
        DescriptorSetLayoutBinding[] bindings)
    {
        _ctx = ctx;
        _device = device;
        using var shaderModule =
            new VkShaderModule(_ctx, _device, spvPath);
        _layout = new VkSetLayout(_ctx, _device, bindings);
        _computePipeline = new VkComputePipeline(_ctx, _device,
            new VkShaderInfo(shaderModule, "main"), [_layout], [
                new PushConstantRange(ShaderStageFlags.ComputeBit, 0,
                    (uint)Marshal.SizeOf<T>())
            ]);
        var bufferCount = (uint)bindings
            .Count(z => z.DescriptorType ==
                        DescriptorType.StorageBuffer);
        var imagesCount = (uint)bindings
            .Count(z => z.DescriptorType ==
                        DescriptorType.StorageImage);
        var descriprotSizes = new List<DescriptorPoolSize>();

        if (bufferCount > 0)
            descriprotSizes.Add(
                new DescriptorPoolSize()
                {
                    Type = DescriptorType.StorageBuffer,
                    DescriptorCount = bufferCount,
                });

        if (imagesCount > 0)
            descriprotSizes.Add(
                new DescriptorPoolSize()
                {
                    Type = DescriptorType.StorageImage,
                    DescriptorCount = imagesCount,
                });


        _descriptorPool = new VkDescriptorPool(_ctx, _device, [
            .. descriprotSizes
        ], 1);
        _descriptorSet =
            _descriptorPool.AllocateDescriptors(_layout, 1)[0];
    }

    public void RecordDispatch(VkCommandRecordingScope recording,
        uint threadGroupCountX,
        uint threadGroupCountY,
        uint threadGroupCountZ)
    {
        var bufferBarriers = new List<BufferMemoryBarrier>();
        foreach (var (binding, (buffer, dstAccesFlags)) in
                 _bufferBindings)
        {
            var barrier = new BufferMemoryBarrier()
            {
                Buffer = buffer.Buffer,
                DstAccessMask = dstAccesFlags,
                Offset = 0,
                Size = buffer.Size,
                SType = StructureType.BufferMemoryBarrier,
                SrcAccessMask = AccessFlags.None
            };
            if (recording.BuffersScope.TryGetValue(buffer,
                    out var srcAccessFlag))
            {
                if (srcAccessFlag == AccessFlags.ShaderReadBit &&
                    dstAccesFlags == AccessFlags.ShaderReadBit)
                    continue;
                barrier.SrcAccessMask = srcAccessFlag;
            }

            bufferBarriers.Add(barrier);
        }

        foreach (var (binding, (buffer, dstAccesFlags)) in
                 _bufferBindings)
        {
            recording.BuffersScope[buffer] = dstAccesFlags;
        }

        var imageBarriers = new List<ImageMemoryBarrier>();
        foreach (var (binding, (image, dstAccessMask)) in
                 _imageBindings)
        {
            var barrier = new ImageMemoryBarrier()
            {
                SType = StructureType.ImageMemoryBarrier,
                DstAccessMask = dstAccessMask,
                Image = image.Image.Image,
                OldLayout = ImageLayout.General,
                NewLayout = ImageLayout.General,
                SubresourceRange = new ImageSubresourceRange(
                    ImageAspectFlags.ColorBit,
                    0, 1, 0, 1)
            };
            if (recording.ImageScope.TryGetValue(image,
                    out var srcAccessFlag))
            {
                if (srcAccessFlag == AccessFlags.ShaderReadBit)
                    continue;
                barrier.SrcAccessMask = srcAccessFlag;
            }

            imageBarriers.Add(barrier);
        }

        foreach (var (binding, (buffer, dstAccessFlags)) in
                 _bufferBindings)
        {
            recording.BuffersScope[buffer] = dstAccessFlags; 
        }

        foreach (var (binding, (image, dstAccessFlags)) in
                 _imageBindings)
        {
            recording.ImageScope[image] = dstAccessFlags;
        }

        if (bufferBarriers.Count > 0 || imageBarriers.Count > 0)
            recording.PipelineBarrier(
                PipelineStageFlags.ComputeShaderBit,
                PipelineStageFlags.ComputeShaderBit,
                DependencyFlags.None,
                bufferMemoryBarriers: [.. bufferBarriers],
                imageMemoryBarriers: [.. imageBarriers]);

        recording.BindPipeline(_computePipeline);
        recording.BindDescriptorSets(PipelineBindPoint.Compute,
            _computePipeline.PipelineLayout, [_descriptorSet]);
        recording.SetPushConstant(_computePipeline,
            ShaderStageFlags.ComputeBit, ref _pushConstant);
        recording.Dispatch(threadGroupCountX, threadGroupCountY,
            threadGroupCountZ);
    }

    public void SetPushConstant(T pushConstant)
    {
        _pushConstant = pushConstant;
    }

    public void SetBufferStorage<TBuf>(int binding,
        VkBuffer<TBuf> buffer,
        AccessFlags accessFlags)
        where TBuf : unmanaged
    {
        _bufferBindings[binding] = (buffer, accessFlags);
        new VkDescriptorSetUpdater(_ctx, _device)
            .AppendWrite(_descriptorSet, binding,
                DescriptorType.StorageBuffer,
                [
                    new DescriptorBufferInfo()
                    {
                        Buffer = buffer.Buffer,
                        Offset = 0,
                        Range = buffer.Size
                    },
                ]
            ).Update();
    }

    public void SetImageStorage(int binding,
        VkImageView view,
        AccessFlags accessFlags)
    {
        _imageBindings[binding] = (view, accessFlags);
        new VkDescriptorSetUpdater(_ctx, _device)
            .AppendWrite(_descriptorSet, binding,
                DescriptorType.StorageImage,
                [
                    new DescriptorImageInfo()
                    {
                        ImageLayout = ImageLayout.General,
                        ImageView = view.ImageView
                    },
                ]
            ).Update();
    }

    public void Dispose()
    {
        _descriptorPool?.Dispose();
        _layout?.Dispose();
        _computePipeline?.Dispose();
    }
}