using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class Haste : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HASTE,
            BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
            MaxStacks = 10
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        Particle _hasteParticle;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            StatsModifier.MoveSpeed.PercentBonus = buff.Variables.GetFloat("hastePercent");
            unit.AddStatModifier(StatsModifier);
            _hasteParticle = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "Global_Haste", unit, buff.Duration);
            _hasteParticle = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "Global_Haste_buf", unit, buff.Duration);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_hasteParticle);
        }
    }
}