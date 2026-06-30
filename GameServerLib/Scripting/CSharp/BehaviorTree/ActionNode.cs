namespace GameServerCore.Scripting.CSharp.BehaviorTree
{
    public class ActionNode : Node
    {
        private Func<NodeState> _action;

        public ActionNode(Func<NodeState> action)
        {
            _action = action;
        }

        public override NodeState Evaluate()
        {
            State = _action?.Invoke() ?? NodeState.Failure;
            return State;
        }
    }
}
