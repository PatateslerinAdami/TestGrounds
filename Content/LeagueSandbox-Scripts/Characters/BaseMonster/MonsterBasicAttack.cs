using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells
{
    public class MonsterBasicAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata();

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            if (owner is Monster mc && !mc.IsMelee)
            {
                ScriptMetadata.MissileParameters = new MissileParameters
                {
                    Type = MissileType.Target
                };
            }
            else
            {
                ScriptMetadata.MissileParameters = null;
            }
        }
    }
}