namespace GameServerCore.Scripting.CSharp.BehaviorTree
{
    public abstract class Node
    {
        public NodeState State { get; protected set; }
        public abstract NodeState Evaluate();
    }
}
