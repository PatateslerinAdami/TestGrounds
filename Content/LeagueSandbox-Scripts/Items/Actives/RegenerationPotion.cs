using System.Numerics;
using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class RegenerationPotion : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var recovery = owner.GetBuffWithName("RegenerationPotion");
            if (recovery != null && !recovery.Elapsed())
            {
                recovery.ExtendDuration(15);
            }
            else
            {
                AddBuff("RegenerationPotion", 15, 1, spell, owner, owner);
            }
        }
    }
}
