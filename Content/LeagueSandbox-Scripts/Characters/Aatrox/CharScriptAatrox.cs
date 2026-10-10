using GameServerCore.Scripting.CSharp;
using Buffs;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts
{
    public class CharScriptAatrox : ICharScript
    {
        private ObjAIBase _owner;
        private int _attackStage;

        private bool Active(string name)
        {
            return _owner.GetBuffWithName(name) is { Remove: false };
        }

        public void OnActivate(ObjAIBase owner, Spell spell = null)
        {
            _owner = owner;
            if (!Active("AatroxPassive")
                && !Active("AatroxPassiveReady")
                && !Active("AatroxPassiveActivate")
                && !Active("AatroxPassiveDeath"))
            {
                owner.Stats.CurrentMana = 0;
            }

            if (!Active("AatroxPassive"))
            {
                AddBuff("AatroxPassive", 25000, 1, spell, owner, owner, true);
            }

            if (!Active("AatroxPassiveReady") && !Active("AatroxPassiveActivate") && !Active("AatroxPassiveDeath"))
            {
                AddBuff("AatroxPassiveReady", 25000, 1, spell, owner, owner, true);
            }

            OnUpdate(0);
        }

        public void OnUpdate(float diff)
        {
            if (_owner == null)
            {
                return;
            }

            if (_owner.Spells[1].CastInfo.SpellLevel > 0 && !Active("AatroxWONHBuff"))
            {
                AddBuff("AatroxWONHBuff", 25000, 1, _owner.Spells[1], _owner, _owner, true);
                if (!Active("AatroxWLife") && !Active("AatroxWPower"))
                {
                    AddBuff(_owner.Spells[1].SpellName == "AatroxW2" ? "AatroxWPower" : "AatroxWLife", 25000, 1, _owner.Spells[1], _owner,
                        _owner, true);
                }
            }
        }

        public Spell SelectAutoAttack(ObjAIBase owner, bool isCritical)
        {
            if (_owner == null)
            {
                return null; 
            }

            if (owner.GetBuffWithName("AatroxWONHBuff") is { Remove: false, BuffScript: AatroxWONHBuff w } && w.Ready)
            {
                return owner.GetSpell(Active("AatroxWPower") ? "AatroxWONHAttackPower" : "AatroxWONHAttackLife");
            }

            if (isCritical)
            {
                return owner.GetAutoAttackSpell(Active("AatroxR") ? "AatroxCritAttack2" : "AatroxCritAttack");
            }

            return owner.GetAutoAttackSpell(Active("AatroxR") ? (_attackStage == 0 ? "AatroxBasicAttack4" : "AatroxBasicAttack5") : (_attackStage == 0 ? "AatroxBasicAttack" : "AatroxBasicAttack2"));
        }

        public void AdvanceAttack()
        {
            _attackStage = (_attackStage + 1) % 2;
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell = null)
        {
            foreach (var name in new[] { "AatroxWONHBuff", "AatroxWLife", "AatroxWPower", "AatroxPassive" }

            )
            {
                owner.RemoveBuffsWithName(name);
            }
        }
    }
}
