using System.Runtime.InteropServices;

namespace FluidsVulkan.ComputeScheduling;

public class Tarjan<TNode>
{
    private readonly Dictionary<TNode, NodeState> _statesBuffer =
        new();

    public bool TopologicalSort(
        List<TNode> nodes,
        Func<TNode, List<TNode>> getNeighbors,
        List<TNode> topSort)
    {
        _statesBuffer.Clear();
        foreach (var node in nodes)
            _statesBuffer.Add(node, NodeState.White);

        while (true)
        {
            var nodeToSearch = _statesBuffer
                .Where(z => z.Value == NodeState.White)
                .Select(z => z.Key)
                .FirstOrDefault();
            if (nodeToSearch == null) break;

            if (!TarjanDepthSearch(nodeToSearch, _statesBuffer,
                    topSort,
                    getNeighbors))
                return false;
        }

        return true;
    }

    private static bool TarjanDepthSearch(TNode node,
        Dictionary<TNode, NodeState> states,
        List<TNode> topSort,
        Func<TNode, List<TNode>> getNeighbors)
    {
        if (states[node] == NodeState.Gray) return false;
        if (states[node] == NodeState.Black) return true;
        states[node] = NodeState.Gray;

        var outgoingNodes = getNeighbors(node);
        foreach (var nextNode in outgoingNodes)
            if (!TarjanDepthSearch(nextNode, states, topSort,
                    getNeighbors))
                return false;

        states[node] = NodeState.Black;
        topSort.Add(node);
        return true;
    }

    private enum NodeState
    {
        White,
        Gray,
        Black,
    }
}

public class Kahn<TNode>
{
    private readonly Dictionary<TNode, int> _degrees = new();
    private readonly Queue<TNode> _queue = new();

    public bool TopologicalSort(
        List<TNode> nodes,
        Func<TNode, List<TNode>> getNeighbors,
        List<TNode> topSort)
    {
        _degrees.Clear();
        _queue.Clear();
        foreach (var node in nodes)
            _degrees[node] = 0;

        foreach (var node in nodes)
        foreach (var neigh in getNeighbors(node))
        {
            ref var degree =
                ref CollectionsMarshal.GetValueRefOrNullRef(_degrees,
                    neigh);
            degree++;
        }

        foreach (var node in nodes)
            if (_degrees[node] == 0)
                _queue.Enqueue(node);

        while (_queue.Count > 0)
        {
            var current = _queue.Dequeue();
            topSort.Add(current);

            foreach (var node in getNeighbors(current))
            {
                ref var degree =
                    ref CollectionsMarshal.GetValueRefOrNullRef(_degrees,
                        node);
                degree--;
                if (degree == 0)
                    _queue.Enqueue(node);
            }
        }

        topSort.Reverse();
        return topSort.Count == nodes.Count;
    }
}

ref struct SpanStack<TNode>(ref Span<TNode> span)
{
    private readonly Span<TNode> _span = span;
    private int _currentIndex = 0;
    
    public int Count => _currentIndex;

    public void Push(TNode node)
    {
        _span[_currentIndex++] = node;
    }
    
    public TNode Pop()
    {
        return _span[_currentIndex--];
    }
}