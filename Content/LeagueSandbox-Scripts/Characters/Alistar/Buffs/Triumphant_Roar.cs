using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Buffs
{
    internal class Triumphant_Roar : IBuffGameScript {
        private ObjAIBase _alistar;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.HEAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _alistar = buff.SourceUnit;
            var ap         = _alistar.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
            var healAmount = ownerSpell.SpellData.EffectLevelAmount[2][ownerSpell.CastInfo.SpellLevel] + ap;
            AddParticleTarget(_alistar, unit, "Meditate_eff", unit, buff.Duration);
            unit.TakeHeal(unit, unit == _alistar ? healAmount : healAmount *0.33f, HealType.SelfHeal);
        }
    }
}