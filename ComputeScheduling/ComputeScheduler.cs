using FluidsVulkan.Vulkan;
using Silk.NET.Vulkan;

namespace FluidsVulkan.ComputeScheduling;

public class ComputeScheduler
{
    private static readonly Lock SyncRoot = new();
    private static ComputeScheduler _instance;

    public static ComputeScheduler Instance
    {
        get
        {
            if (_instance != null)
                return _instance;
            lock (SyncRoot)
            {
                _instance ??= new ComputeScheduler();
            }

            return _instance;
        }
    }

    private readonly DependencyGraphBuilder _dependencyGraphBuilder =
        new();


    private ComputeRecorder _computeRecorder = new();

    public void AddTask(IComputeTask task)
    {
        _dependencyGraphBuilder.AddTask(task);
    }

    private Kahn<IGpuTask> _graphUtils = new();
    private List<IGpuTask> _topological = new();

    public async Task RecordAll(
        VkCommandRecordingScope scope)
    {
        // Console.WriteLine(_dependencyGraphBuilder.Tasks.Count);
        // // var neigbours = _dependencyGraphBuilder.Tasks
        // //     .AsParallel()
        // //     .Select(
        // //         z => (z, _dependencyGraphBuilder.GetNeighbours(z)))
        // //     .ToDictionary(z => z.Item1, z => z.Item2);
        // //
        // // ///var neighbours = await Task.WhenAll(tasks);
        _topological.Clear();
        var neigbours = _dependencyGraphBuilder.BuildGraph();
        var topological =
            _graphUtils.TopologicalSort(
                _dependencyGraphBuilder.Tasks,
                z =>
                    neigbours[z], _topological);
        
        _computeRecorder.Record(scope,
            _topological);
        _dependencyGraphBuilder.Clear();
        
    }
}

public class ComputeRecorder :
    IComputeResourceVisitor<(BufferMemoryBarrier?,
        VkImageMemoryBarrier?)>
{
    private Dictionary<IVkBuffer, AccessFlags>
        _bufferAccessFlags = [];

    private Dictionary<VkImage, (AccessFlags, ImageLayout)>
        _imageAccessFlags =
            [];

    private PipelineStageFlags _pipelineStage =
        PipelineStageFlags.TopOfPipeBit;

    public void Record(VkCommandRecordingScope scope,
        List<IGpuTask> tasks)
    {
        _pipelineStage = PipelineStageFlags.TopOfPipeBit;
        _bufferAccessFlags.Clear();
        _imageAccessFlags.Clear();

        var bufferBarriers = new List<BufferMemoryBarrier>();
        var imageBarriers = new List<VkImageMemoryBarrier>();
        var hashset =
            new HashSet<IComputeResource>(bufferBarriers.Count +
                                          imageBarriers.Count);

        foreach (var task in tasks)
        {
            bufferBarriers.Clear();
            imageBarriers.Clear();
            hashset.Clear();
            imageBarriers.Capacity = bufferBarriers.Capacity =
                task.Reads.Count + task.Writes.Count;

            foreach (var resource in task.Reads)
                hashset.Add(resource);

            foreach (var resource in task.Writes)
                hashset.Add(resource);

            foreach (var resource in hashset)
            {
                var (bufferMemoryBarrier, imageBarrier) =
                    resource.Accept(this);

                if (imageBarrier != null)
                    imageBarriers.Add(imageBarrier.Value);
                if (bufferMemoryBarrier != null)
                    bufferBarriers.Add(bufferMemoryBarrier.Value);
            }

            if (bufferBarriers.Count > 0 || imageBarriers.Count > 0 ||
                task.PipelineStage != _pipelineStage)
            {
                scope.PipelineBarrier(_pipelineStage,
                    task.PipelineStage,
                    DependencyFlags.None,
                    bufferMemoryBarriers: [.. bufferBarriers],
                    imageMemoryBarriers: [.. imageBarriers]);
            }

            _pipelineStage = task.InvokeRecord(scope);
        }
    }

    public (BufferMemoryBarrier?, VkImageMemoryBarrier?) Visit(
        BufferResource resource)
    {
        var buffer = resource.Buffer;
        if (!_bufferAccessFlags.TryGetValue(buffer,
                out var srcAccessFlag))
        {
            _bufferAccessFlags[buffer] = resource.AccessFlags;
            return (null, null);
        }

        if (srcAccessFlag == AccessFlags.ShaderReadBit &&
            resource.AccessFlags == AccessFlags.ShaderReadBit)
            return (null, null);

        var bufferMemoryBarrier = new BufferMemoryBarrier()
        {
            Buffer = buffer.Buffer,
            DstAccessMask = resource.AccessFlags,
            SrcAccessMask =
                CheckSupport(_pipelineStage, srcAccessFlag)
                    ? AccessFlags.None
                    : srcAccessFlag,
            Offset = 0,
            SType = StructureType.BufferMemoryBarrier,
            Size = buffer.Size
        };

        _bufferAccessFlags[buffer] = resource.AccessFlags;

        return (bufferMemoryBarrier, null);
    }

    private static bool CheckSupport(
        PipelineStageFlags pipelineStageFlags,
        AccessFlags accessFlags)
    {
        return pipelineStageFlags switch
        {
            PipelineStageFlags.TopOfPipeBit => false,
            PipelineStageFlags.TransferBit => accessFlags is
                AccessFlags.TransferReadBit
                or AccessFlags.TransferWriteBit,
            _ => true
        };
    }

    public (BufferMemoryBarrier?, VkImageMemoryBarrier?) Visit(
        ImageResource resource)
    {
        var image = resource.Image;
        if (!_imageAccessFlags.TryGetValue(image,
                out var val))
        {
            _imageAccessFlags[image] = (AccessFlags.None,
                image.LastLayout);
            val = _imageAccessFlags[image];
        }

        var (srcAccessFlags, srcLayout) = val;
        if (srcAccessFlags == AccessFlags.ShaderReadBit &&
            resource.AccessFlags == AccessFlags.ShaderReadBit &&
            srcLayout == resource.Layout)
            return (null, null);
        var imageMemoryBarrier = new VkImageMemoryBarrier()
        {
            Image = image,
            DstAccessMask = resource.AccessFlags,
            SrcAccessMask =
                CheckSupport(_pipelineStage, srcAccessFlags)
                    ? AccessFlags.None
                    : srcAccessFlags,
            NewLayout = resource.Layout,
            SubresourceRange = new ImageSubresourceRange(
                ImageAspectFlags.ColorBit,
                0, 1, 0, 1)
        };

        _imageAccessFlags[image] =
            (resource.AccessFlags, resource.Layout);
        return (null, imageMemoryBarrier);
    }
}