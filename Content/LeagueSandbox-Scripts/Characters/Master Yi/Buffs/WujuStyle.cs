using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameMaths;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs
{
    internal class WujuStyle: IBuffGameScript
    {

        private ObjAIBase _masterYi;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };
        // Must be initialized (was null -> NRE on the FlatBonus assignment below).
        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _masterYi = ownerSpell.CastInfo.Owner;
            var bonusAd = _masterYi.Stats.AttackDamage.Total * ownerSpell.SpellData.EffectLevelAmount[1][ownerSpell.CastInfo.SpellLevel]/100;
            StatsModifier.AttackDamage.FlatBonus += bonusAd;
            // Actually apply the bonus AD to the unit (was never applied before).
            unit.AddStatModifier(StatsModifier);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}