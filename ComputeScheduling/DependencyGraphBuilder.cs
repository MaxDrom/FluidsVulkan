namespace FluidsVulkan.ComputeScheduling;

public class DependencyGraphBuilder
{
    private readonly Dictionary<ulong, IGpuTask> _lastWriter = [];

    private readonly Dictionary<IGpuTask, List<IGpuTask>> _neighbours =
        [];

    private readonly Dictionary<ulong, List<IGpuTask>> _lastReaders = [];

    private readonly ResourceHasher _resourceHasher = new();
    public List<IGpuTask> Tasks { get; } = [];

    public void AddTask(IGpuTask task)
    {
        Tasks.Add(task);
        _neighbours[task] = new List<IGpuTask>();
        foreach (var resource in task.Writes)
        {
            var id = resource.Accept(_resourceHasher);
            if (_lastReaders.TryGetValue(id, out var readers))
            {
                foreach (var reader in readers)
                    _neighbours[task].Add(reader);
                _lastReaders.Clear();
            }

            if (_lastWriter.TryGetValue(id, out var lastWriter))
                _neighbours[task].Add(lastWriter);

            _lastWriter[id] = task;
        }

        foreach (var resource in task.Reads)
        {
            var id = resource.Accept(_resourceHasher);
            if (_lastWriter.TryGetValue(id, out var lastWriter) &&
                lastWriter != task)
                _neighbours[task].Add(lastWriter);
            if (!_lastReaders.ContainsKey(id))
                _lastReaders[id] = [];
            _lastReaders[id].Add(task);
        }
    }

    public Dictionary<IGpuTask, List<IGpuTask>> BuildGraph()
    {
        return _neighbours;
    }

    public void Clear()
    {
        Tasks.Clear();
        _lastReaders.Clear();
        _lastWriter.Clear();
        _neighbours.Clear();
    }
}

public class ResourceHasher : IComputeResourceVisitor<ulong>
{
    public ulong Visit(BufferResource resource)
    {
        return resource.Buffer.Buffer.Handle;
    }

    public ulong Visit(ImageResource resource)
    {
        return resource.Image.Image.Handle;
    }
}