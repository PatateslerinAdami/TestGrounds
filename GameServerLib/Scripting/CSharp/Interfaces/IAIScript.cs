using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace GameServerCore.Scripting.CSharp
{
    public interface IAIScript
    {
        AIScriptMetaData AIScriptMetaData { get; set; }
        void OnActivate(ObjAIBase owner)
        {
        }

        void OnUpdate(float diff)
        {
        }

        void OnCallForHelp(AttackableUnit attacker, AttackableUnit victium)
        {
        }

        // Riot's forced/important Call For Help (DamageEffect::ForceCallForHelp). Only Turret.lua reacts
        // (focus-lock = tower-dive aggro); default is a no-op for every other archetype.
        void OnReceiveImportantCallForHelp(AttackableUnit attacker, AttackableUnit victium)
        {
        }
    }
}