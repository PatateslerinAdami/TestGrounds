using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts
{
    public class CharScriptFiora : ICharScript
    {
        private ObjAIBase _owner;
        private Spell _passive;
        private Buff _duelist, _riposte;

        public void OnActivate(ObjAIBase owner, Spell spell = null)
        {
            if (_owner != null)
            {
                return;
            }

            _owner = owner;
            _passive = spell;
            _duelist = AddBuff("FioraDuelist", 25000, 1, spell, owner, owner, infiniteduration: true);
            _riposte = AddBuff("FioraRiposteBuff", 25000, 1, owner.Spells[1], owner, owner, infiniteduration: true);
            ApiEventManager.OnDealDamage.AddListener(this, owner, OnDamage);
            ApiEventManager.OnDeath.AddListener(this, owner, _ => RemoveTemporaryBuffs());
        }

        private void OnDamage(DamageData data)
        {
            if (_owner.IsDead)
            {
                return;
            }

            if (data.Attacker != _owner)
            {
                return;
            }

            if (data.Target == null)
            {
                return;
            }

            if (data.Target.Team == _owner.Team)
            {
                return;
            }

            if (data.DamageType != DamageType.DAMAGE_TYPE_PHYSICAL)
            {
                return;
            }

            if (data.Damage <= 0)
            {
                return;
            }

            if (data.PostMitigationDamage <= 0)
            {
                return;
            }

            if (data.IsHitBlocked)
            {
                return;
            }

            if (data.DamageResultType is DamageResultType.RESULT_MISS or DamageResultType.RESULT_DODGE or DamageResultType.RESULT_INVULNERABLE or DamageResultType.RESULT_INVULNERABLENOMESSAGE)
            {
                return;
            }

            var hot = _owner.GetBuffWithName("FioraDuelistHot");
            if (hot == null || hot.Remove)
            {
                hot = AddBuff("FioraDuelistHot", 6, 1, _passive, _owner, _owner);
            }
            else
            {
                if (data.Target is Champion)
                {
                    hot.SetStacks(System.Math.Min(4, hot.StackCount + 1));
                }

                hot.Refresh();
            }

            (hot?.BuffScript as Buffs.FioraDuelistHot)?.SyncRegeneration();
        }

        private void RemoveTemporaryBuffs()
        {
            foreach (var name in new[] { "FioraDuelistHot", "FioraRiposte", "FioraFlurry", "FioraFlurryDummy" }

            )
            {
                _owner.RemoveBuffsWithName(name);
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell = null)
        {
            if (_owner == null)
            {
                return;
            }

            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveTemporaryBuffs();
            if (_duelist != null)
            {
                owner.RemoveBuff(_duelist);
            }

            if (_riposte != null)
            {
                owner.RemoveBuff(_riposte);
            }

            _owner = null;
        }
    }
}
