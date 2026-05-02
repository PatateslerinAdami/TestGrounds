using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;

namespace Buffs
{
    internal class YasuoCritMod : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private ObjAIBase _owner;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = unit as ObjAIBase;

            if (_owner != null)
            {
                ApiEventManager.OnUpdateStats.AddListener(this, _owner, OnUpdateStats, false);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnUpdateStats.RemoveListener(this);
        }

        private void OnUpdateStats(AttackableUnit unit, float diff)
        {
            if (_owner == null) return;

            // Cleanup previous modifier to recalculate
            _owner.RemoveStatModifier(StatsModifier);
            StatsModifier = new StatsModifier();
            StatsModifier.CriticalChance.FlatBonus = _owner.Stats.CriticalChance.Total;

            // Yasuo's passive: Reduce Critical Strike Damage by 10%
            StatsModifier.CriticalDamage.PercentBonus = -0.10f;

            _owner.AddStatModifier(StatsModifier);
        }
    }
}