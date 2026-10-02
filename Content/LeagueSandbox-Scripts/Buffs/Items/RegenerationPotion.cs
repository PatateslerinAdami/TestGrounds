using GameServerCore.Enums;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    internal class RegenerationPotion : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HEAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 5
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private Buff _buff;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            StatsModifier.HealthRegeneration.FlatBonus = 10f;
            unit.AddStatModifier(StatsModifier);
        }

        public void OnUpdate(float diff)
        {
            if (_buff.TargetUnit.IsDead)
            {
                RemoveBuff(_buff);
            }
        }
    }
}
