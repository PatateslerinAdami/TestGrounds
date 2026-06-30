namespace GameServerCore.Scripting.CSharp.BehaviorTree
{
    public class Sequence : Node
    {
        private List<Node> _nodes = new List<Node>();

        public Sequence(params Node[] nodes)
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
                        State = NodeState.Failure;
                        return State; 
                    case NodeState.Success:
                        continue; 
                    case NodeState.Running:
                        State = NodeState.Running;
                        return State; 
                }
            }
            State = NodeState.Success;
            return State; 
        }
    }
}
