namespace GameServerCore.Scripting.CSharp.BehaviorTree
{
    public class Selector : Node
    {
        private List<Node> _nodes = new List<Node>();

        public Selector(params Node[] nodes)
        {
            _nodes.AddRange(nodes);
        }

        public override NodeState Evaluate()
        {
            foreach (var node in _nodes)
            {
                switch (node.Evaluate())
                {
                    case NodeState.Failure:
                        continue; 
                    case NodeState.Success:
                        State = NodeState.Success;
                        return State; 
                    case NodeState.Running:
                        State = NodeState.Running;
                        return State; 
                }
            }
            State = NodeState.Failure;
            return State; 
        }
    }
}
