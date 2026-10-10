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
    public class AatroxEConeMissile : IBuffGameScript
    {
        private Particle _effect;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType= BuffType.SLOW,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier{ get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            BuffMetaData.ClientPackageHash = owner.GetObjHash();
            _effect = AddParticleTarget(owner, unit, Spells.AatroxHelpers.Effect(owner, "E_Slow"), unit);
            StatsModifier.MoveSpeed.PercentBonus = -0.4f;
            unit.AddStatModifier(StatsModifier);
            ApplyAssistMarker(unit, owner, 10);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_effect);
        }
    }

    public class AatroxEslow : AatroxEConeMissile
    {
    }
}
