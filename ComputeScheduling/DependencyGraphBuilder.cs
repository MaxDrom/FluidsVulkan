using FluidsVulkan.ComputeSchduling;

namespace FluidsVulkan.ComputeScheduling;

public class DependencyGraphBuilder
{
    private readonly Dictionary<IComputeTask, int> _taskIds = new();


    public List<IComputeTask> Tasks { get; } = [];

    public void AddTask(IComputeTask task)
    {
        Tasks.Add(task);
        _taskIds[task] = Tasks.Count - 1;
    }
    
    public IEnumerable<IComputeTask> GetNeighbours(IComputeTask task)
    {
        var i = _taskIds[task];
        
        for (var j = i + 1; j < Tasks.Count; j++)
        {
            if (FindOverlap(Tasks[i], Tasks[j]))
                yield return Tasks[j];
        }
    }

    private bool FindOverlap(
        IComputeTask task1,
        IComputeTask task2)
    {
        foreach (var resourceRead in task1.Reads)
        foreach (var resourceWrite in task2.Writes)
            if (resourceRead.IsOverlap(resourceWrite))
            {
                return true;
            }


        foreach (var resourceRead in task1.Writes)
        foreach (var resourceWrite in task2.Reads)
            if (resourceRead.IsOverlap(resourceWrite))
            {
                return true;
            }


        foreach (var resourceRead in task1.Writes)
        foreach (var resourceWrite in task2.Writes)
            if (resourceRead.IsOverlap(resourceWrite))
            {
                return true;
            }
        
        return false;
    }

    public void Clear()
    {
        Tasks.Clear();
    }
}