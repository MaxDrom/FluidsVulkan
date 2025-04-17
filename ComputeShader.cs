using System.Runtime.InteropServices;
using FluidsVulkan.ComputeScheduling;
using FluidsVulkan.ComputeScheduling.Executors;
using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan;

public class ComputeShader<T>(VkContext ctx,
    VkDevice device,
    string spvPath
)
    : IDisposable
    where T : unmanaged
{
    private VkDescriptorPool _descriptorPool;
    private VkSetLayout _layout;
    private DescriptorSet[] _descriptorSets;
    private VkComputePipeline _computePipeline;
    private T _pushConstant;
    private int _currentSet;

    private Dictionary<int, (IVkBuffer, AccessFlags)>
        _bufferBindings =
            [];

    private Dictionary<int, (VkImageView, AccessFlags)>
        _imageBindings =
            [];

    private void InitPipeline()
    {
        using var shaderModule =
            new VkShaderModule(ctx, device, spvPath);
        var bindings = new List<DescriptorSetLayoutBinding>();
        var bufferCount = _bufferBindings.Count;
        var imagesCount = _imageBindings.Count;
        foreach (var (binding, _) in _bufferBindings)
        {
            bindings.Add(
                new DescriptorSetLayoutBinding()
                {
                    Binding = (uint)binding,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.StorageBuffer,
                    StageFlags = ShaderStageFlags.ComputeBit
                });
        }

        foreach (var (binding, _) in _imageBindings)
        {
            bindings.Add(
                new DescriptorSetLayoutBinding()
                {
                    Binding = (uint)binding,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.StorageImage,
                    StageFlags = ShaderStageFlags.ComputeBit
                });
        }

        _layout = new VkSetLayout(ctx, device, [..bindings]);
        _computePipeline = new VkComputePipeline(ctx, device,
            new VkShaderInfo(shaderModule, "main"), [_layout], [
                new PushConstantRange(ShaderStageFlags.ComputeBit, 0,
                    (uint)Marshal.SizeOf<T>())
            ]);

        var descriprotSizes = new List<DescriptorPoolSize>();

        if (bufferCount > 0)
            descriprotSizes.Add(
                new DescriptorPoolSize()
                {
                    Type = DescriptorType.StorageBuffer,
                    DescriptorCount = (uint)bufferCount * 1000,
                });

        if (imagesCount > 0)
            descriprotSizes.Add(
                new DescriptorPoolSize()
                {
                    Type = DescriptorType.StorageImage,
                    DescriptorCount = (uint)imagesCount * 1000,
                });


        _descriptorPool = new VkDescriptorPool(ctx, device, [
            .. descriprotSizes
        ], 1000);
        _descriptorSets =
            _descriptorPool.AllocateDescriptors(_layout, 1000);
    }

    public void Dispatch(uint threadGroupCountX,
        uint threadGroupCountY,
        uint threadGroupCountZ)
    {
        if (_computePipeline == null)
            InitPipeline();
        var updater = new VkDescriptorSetUpdater(ctx, device);
        foreach (var (binding, (buffer, _)) in _bufferBindings)
        {
            updater = updater
                .AppendWrite(_descriptorSets[_currentSet], binding,
                    DescriptorType.StorageBuffer,
                    [
                        new DescriptorBufferInfo()
                        {
                            Buffer = buffer.Buffer,
                            Offset = 0,
                            Range = buffer.Size
                        },
                    ]
                );
        }

        foreach (var (binding, (image, _)) in _imageBindings)
        {
            updater = updater
                .AppendWrite(_descriptorSets[_currentSet], binding,
                    DescriptorType.StorageImage,
                    [
                        new DescriptorImageInfo()
                        {
                            ImageLayout = ImageLayout.General,
                            ImageView = image.ImageView
                        },
                    ]
                );
        }

        updater.Update();


        var reads = new List<IComputeResource>();
        var writes = new List<IComputeResource>();
        foreach (var (_, (buffer, accessFlags)) in _bufferBindings)
        {
            if (accessFlags.HasFlag(AccessFlags.ShaderReadBit))
                reads.Add(new BufferResource()
                {
                    Buffer = buffer,
                    AccessFlags = accessFlags
                });

            if (accessFlags.HasFlag(AccessFlags.ShaderWriteBit))
                writes.Add(new BufferResource()
                {
                    Buffer = buffer,
                    AccessFlags = accessFlags
                });
        }

        foreach (var (_, (image, accessFlags)) in _imageBindings)
        {
            if (accessFlags.HasFlag(AccessFlags.ShaderReadBit))
                reads.Add(new ImageResource()
                {
                    Image = image.Image,
                    AccessFlags = accessFlags,
                    Layout = ImageLayout.General
                });

            if (accessFlags.HasFlag(AccessFlags.ShaderWriteBit))
                writes.Add(new ImageResource()
                {
                    Image = image.Image,
                    AccessFlags = accessFlags,
                    Layout = ImageLayout.General
                });
        }

        ComputeScheduler.Instance.AddTask(new DispatchTask()
        {
            Reads = reads,
            Writes = writes,
            Executor = new DispatchExecutor<T>()
            {
                PushConstant = _pushConstant,
                DescriptorSet = _descriptorSets[_currentSet],
                NumGroupsX = threadGroupCountX,
                NumGroupsY = threadGroupCountY,
                NumGroupsZ = threadGroupCountZ,
                Pipeline = _computePipeline
            }
        });

        _currentSet = (++_currentSet) % 1000;
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
    }

    public void SetImageStorage(int binding,
        VkImageView view,
        AccessFlags accessFlags)
    {
        _imageBindings[binding] = (view, accessFlags);
    }

    public void Dispose()
    {
        _descriptorPool?.Dispose();
        _layout?.Dispose();
        _computePipeline?.Dispose();
    }
}