using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class RivenPassiveWatcher : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            IsHidden = true,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private bool _subscribed;
        private ObjAIBase _owner;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (!(unit is ObjAIBase owner)) return;
            _owner = owner;

            if (!_subscribed)
            {
                ApiEventManager.OnSpellCast.AddListener(this, ownerSpell, OnSpellCast);
                _subscribed = true;
            }
        }

        public void OnDeactivate(AttackableUnit unit)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }

        public void OnUpdate(float diff)
        {
        }

        private void OnSpellCast(Spell spell)
        {
            string spellName = spell.SpellName;

            bool isAbility = spellName == "RivenTriCleave"
                          || spellName == "RivenMartyr"
                          || spellName == "RivenFeint"
                          || spellName == "RivenFengShuiEngine"
                          || spellName == "RivenIzunaBlade";

            if (!isAbility) return;

            var existing = _owner.GetBuffWithName("RivenPassiveAABoost");
            if (existing != null)
            {
                int currentStacks = existing.StackCount;
                if (currentStacks < 3)
                {
                    existing.IncrementStackCount();
                }
                existing.ResetTimeElapsed();
            }
            else
            {
                AddBuff("RivenPassiveAABoost", 5f, 1, spell, _owner, _owner);
            }
        }
    }
}
