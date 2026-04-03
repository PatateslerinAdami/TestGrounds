using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class AatroxW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = false
        };

        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            if (spell.CastInfo.SpellLevel == 0)
            {
                ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell, true);
            }
        }

        private void OnLevelUpSpell(Spell spell)
        {
            AddBuff("AatroxWLife", 25000f, 1, spell, _owner, _owner, true);
            ApiEventManager.OnLevelUpSpell.RemoveListener(this, spell);
        }

        public void OnSpellCast(Spell spell)
        {
            if (_owner.GetBuffWithName("AatroxWLife") is Buff b)
            {
                b.DeactivateBuff();
            }

            _owner.RegisterTimer(new GameScriptTimer(0.01f, () =>
            {
                AddBuff("AatroxWPower", 25000f, 1, spell, _owner, _owner, true);
                _owner.GetSpell("AatroxW2").SetCooldown(0.5f, true);
            }));
        }
    }
    public class AatroxW2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = false
        };

        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }

        public void OnSpellCast(Spell spell)
        {
            if (_owner.GetBuffWithName("AatroxWPower") is Buff b)
            {
                b.DeactivateBuff();
            }

            _owner.RegisterTimer(new GameScriptTimer(0.01f, () =>
            {
                AddBuff("AatroxWLife", 1f, 1, spell, _owner, _owner, true);
                _owner.GetSpell("AatroxW").SetCooldown(0.5f, true);
            }));
        }
    }
}