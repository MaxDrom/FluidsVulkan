namespace FluidsVulkan.ComputeSchduling;

public class DependencyEdge
{
    public IComputeTask From { get; set; }
    public IComputeTask To { get; set; }
}

public class DependencyGraphBuilder
{
    private readonly List<IComputeTask> _tasks = [];

    public void AddTask(IComputeTask task)
    {
        _tasks.Add(task);
    }

    public (List<IComputeTask>,
        Dictionary<IComputeTask, List<DependencyEdge>>) Build()
    {
        var edges =
            new Dictionary<IComputeTask, List<DependencyEdge>>();
        foreach (var task in _tasks)
            edges[task] = new List<DependencyEdge>();
        for (var i = 0; i < _tasks.Count-1; i++)
        for (var j = i + 1; j < _tasks.Count; j++)
        {
            var edge = FindOverlap(_tasks[i], _tasks[j]);
            if (edge != null)
                edges[_tasks[i]].Add(edge);
        }

        return (_tasks, edges);
    }

    private DependencyEdge FindOverlap(
        IComputeTask task1,
        IComputeTask task2)
    {
        foreach (var resourceRead in task1.Reads)
        {
            foreach (var resourceWrite in task2.Writes)
            {
                if (resourceRead.IsOverlap(resourceWrite))
                    return new DependencyEdge()
                        { From = task1, To = task2 };
            }
        }

        foreach (var resourceRead in task1.Writes)
        {
            foreach (var resourceWrite in task2.Reads)
            {
                if (resourceRead.IsOverlap(resourceWrite))
                    return new DependencyEdge()
                        { From = task1, To = task2 };
            }
        }

        foreach (var resourceRead in task1.Writes)
        {
            foreach (var resourceWrite in task2.Writes)
            {
                if (resourceRead.IsOverlap(resourceWrite))
                    return new DependencyEdge()
                        { From = task1, To = task2 };
            }
        }

        return null;
    }
}