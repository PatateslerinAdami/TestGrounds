using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class WillRevive : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData();
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Particle _particle;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _particle = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "rebirthready.troy",
                unit,
                float.MaxValue,
                bone: "spine"
            );
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_particle);
        }
    }
}
