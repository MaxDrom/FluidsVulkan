namespace FluidsVulkan.ComputeScheduling;

public class DependencyGraphBuilder
{
    public List<IGpuTask> Tasks { get; } = [];

    private Dictionary<ulong, IGpuTask> _lastWriter = [];
    private Dictionary<ulong, List<IGpuTask>> _readers = [];

    private Dictionary<IGpuTask, List<IGpuTask>> _negbours = [];

    private ResourceHasher _resourceHasher = new();

    public void AddTask(IGpuTask task)
    {
        Tasks.Add(task);
        _negbours[task] = new List<IGpuTask>();
        foreach (var resource in task.Writes)
        {
            var id = resource.Accept(_resourceHasher);
            if (_readers.TryGetValue(id, out var readers))
            {
                foreach (var reader in readers)
                {
                    _negbours[task].Add(reader);
                }
                _readers.Clear();
            }

            if(_lastWriter.TryGetValue(id, out var lastWriter))
                _negbours[task].Add(lastWriter);
            
            _lastWriter[id] = task;
            
        }
        
        foreach (var resource in task.Reads)
        {
            var id = resource.Accept(_resourceHasher);
            if(_lastWriter.TryGetValue(id, out var lastWriter) && lastWriter != task)
                _negbours[task].Add(lastWriter);
            if(!_readers.ContainsKey(id))
                _readers[id] = new List<IGpuTask>();
            _readers[id].Add(task);
        }
    }

    public Dictionary<IGpuTask, List<IGpuTask>> BuildGraph()
    {
        return _negbours;
    }

    public void Clear()
    {
        Tasks.Clear();
        _readers.Clear();
        _lastWriter.Clear();
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