using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class PowerBall : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        Spell _spell;
        ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
        }

        public void OnSpellPostCast(Spell spell)
        {
            if (_owner.GetBuffWithName("PowerBall") is Buff a)
            {
                a.DeactivateBuff();
            }
            else
            {
                if (_owner.GetBuffWithName("DefensiveBallCurl") is Buff b)
                {
                    b.DeactivateBuff();
                }
                AddBuff("PowerBall", 6f, 1, _spell, _owner, _owner);
                _spell.SetCooldown(0.5f, true);
            }
        }
    }
}