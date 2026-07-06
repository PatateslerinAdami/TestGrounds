using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class NunuQBuffGolem : IBuffGameScript
    {

        private ObjAIBase _nunu;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };
        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _nunu = ownerSpell.CastInfo.Owner;
            var pct = ownerSpell.SpellData.EffectLevelAmount[5][ownerSpell.CastInfo.SpellLevel];
            var bonusHealth = _nunu.Stats.HealthPoints.Total * pct;
            StatsModifier.Size.PercentBaseBonus = pct;
            StatsModifier.HealthPoints.FlatBonus = bonusHealth;
            unit.AddStatModifier(StatsModifier);
            // Wiki: "...bonus health as well as healing for the amount gained." AddStatModifier raised
            // MaxHP by bonusHealth first, so this heal fills exactly that new headroom.
            _nunu.TakeHeal(_nunu, bonusHealth, HealType.SelfHeal);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}