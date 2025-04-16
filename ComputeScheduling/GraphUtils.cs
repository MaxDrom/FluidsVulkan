namespace FluidsVulkan.ComputeSchduling;

public class GraphUtils
{
    private enum NodeState
    {
        White,
        Gray,
        Black
    }

    public static List<TNode> TopologicalSort<TNode>(
        IEnumerable<TNode> nodes,
        Func<TNode, IEnumerable<TNode>> getNeighbors)
    {
        var states = nodes.ToDictionary(z => z, z => NodeState.White);
        //var result = new List<List<TNode>>();
        List<TNode> topSort = [];
        while (true)
        {
            var nodeToSearch = states
                .Where(z => z.Value == NodeState.White)
                .Select(z => z.Key)
                .FirstOrDefault();
            if (nodeToSearch == null) break;

            if (!TarjanDepthSearch(nodeToSearch, states, topSort,
                    getNeighbors))
                return null;
        }

        topSort.Reverse();
        return topSort;
    }

    private static bool TarjanDepthSearch<TNode>(TNode node,
        Dictionary<TNode, NodeState> states,
        List<TNode> topSort,
        Func<TNode, IEnumerable<TNode>> getNeighbors)
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
}