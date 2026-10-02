using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace AIScripts
{
    public class OdinPickupAI : IAIScript
    {
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData();

        public void OnActivate(ObjAIBase owner)
        {
            owner.SetStatus(StatusFlags.ForceRenderParticles, true);
        }
    }
}
