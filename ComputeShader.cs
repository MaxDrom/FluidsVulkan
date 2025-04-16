using System.Runtime.InteropServices;
using FluidsVulkan.ComputeSchduling;
using FluidsVulkan.ComputeScheduling;
using Silk.NET.Vulkan;

namespace FluidsVulkan;

public interface IComputeShader
{
    void RecordDispatch(VkCommandRecordingScope recording,
        uint threadGroupCountX,
        uint threadGroupCountY,
        uint threadGroupCountZ,
        object pushConstantBoxed);
}



public class ComputeShader<T>(VkContext ctx,
    VkDevice device,
    string spvPath
)
    : IDisposable, IComputeShader
    where T : unmanaged
{
    private VkDescriptorPool _descriptorPool;
    private VkSetLayout _layout;
    private DescriptorSet _descriptorSet;
    private VkComputePipeline _computePipeline = null;
    private T _pushConstant;

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
                    DescriptorCount = (uint)bufferCount,
                });

        if (imagesCount > 0)
            descriprotSizes.Add(
                new DescriptorPoolSize()
                {
                    Type = DescriptorType.StorageImage,
                    DescriptorCount = (uint)imagesCount,
                });


        _descriptorPool = new VkDescriptorPool(ctx, device, [
            .. descriprotSizes
        ], 1);
        _descriptorSet =
            _descriptorPool.AllocateDescriptors(_layout, 1)[0];
        var updater = new VkDescriptorSetUpdater(ctx, device);
        foreach (var (binding, (buffer, _)) in _bufferBindings)
        {
            updater = updater
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
                );
        }
        foreach (var (binding, (image, _)) in _imageBindings)
        {
            updater = updater
                .AppendWrite(_descriptorSet, binding,
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
    }

    public void Dispatch(uint threadGroupCountX,
        uint threadGroupCountY,
        uint threadGroupCountZ)
    {
        var reads = new List<IComputeResource>();
        var writes = new List<IComputeResource>();
        foreach (var (_, (buffer, accessFlags)) in _bufferBindings)
        {
            if(accessFlags.HasFlag(AccessFlags.ShaderReadBit))
                reads.Add(new BufferResource()
                {
                    Buffer = buffer,
                    AccessFlags = accessFlags
                });
            
            if(accessFlags.HasFlag(AccessFlags.ShaderWriteBit))
                writes.Add(new BufferResource()
                {
                    Buffer = buffer,
                    AccessFlags = accessFlags
                });
        }
        
        foreach (var (_, (image, accessFlags)) in _imageBindings)
        {
            if(accessFlags.HasFlag(AccessFlags.ShaderReadBit))
                reads.Add(new ImageResource()
                {
                    Image = image.Image,
                    AccessFlags = accessFlags,
                    Layout = ImageLayout.General
                });
            
            if(accessFlags.HasFlag(AccessFlags.ShaderWriteBit))
                writes.Add(new ImageResource()
                {
                    Image = image.Image,
                    AccessFlags = accessFlags,
                    Layout = ImageLayout.General
                });
        }
        ComputeScheduler.Instance.AddTask(new DispatchTaks()
        {
            ComputeShader = this,
            NumGroupsX = threadGroupCountX,
            NumGroupsY = threadGroupCountY,
            NumGroupsZ = threadGroupCountZ,
            Reads = reads,
            Writes = writes,
            PushConstant = _pushConstant
        });
    }
    
    public void RecordDispatch(VkCommandRecordingScope recording,
        uint threadGroupCountX,
        uint threadGroupCountY,
        uint threadGroupCountZ,
        object pushConstantBoxed)
    {
        var pushConstant = (T)pushConstantBoxed;
        if(_computePipeline == null)
            InitPipeline();
        
        recording.BindPipeline(_computePipeline);
        recording.BindDescriptorSets(PipelineBindPoint.Compute,
            _computePipeline!.PipelineLayout, [_descriptorSet]);
        recording.SetPushConstant(_computePipeline,
            ShaderStageFlags.ComputeBit, ref pushConstant);
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
        if(_computePipeline == null)
            return;
        new VkDescriptorSetUpdater(ctx, device)
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
        if(_computePipeline == null)
            return;
        new VkDescriptorSetUpdater(ctx, device)
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