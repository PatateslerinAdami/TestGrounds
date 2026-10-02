using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class SpectralFury : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private Buff _buff;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            StatsModifier.MoveSpeed.PercentBonus = 0.2f;
            StatsModifier.AttackSpeed.FlatBonus = 0.4f;
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

    internal class YoumuusGhostblade : SpectralFury
    {
    }
}
