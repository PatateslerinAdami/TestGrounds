using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class FioraQCD : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier
        {
            get
            {
                return null;
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            (buff.ParentScript as Spells.FioraQ)?.OnRecastRemoved(buff);
        }
    }

    public class FioraQDash : IBuffGameScript
    {
        private Particle _trail;

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };

        public StatsModifier StatsModifier
        {
            get
            {
                return null;
            }
        }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            buff.SetStatusEffect(StatusFlags.Ghosted, true);
            _trail = AddParticleTarget(buff.SourceUnit, unit, "FioraQLunge_dashtrail.troy", unit, lifetime: buff.Duration);
            ApiEventManager.OnDeath.AddListener(this, unit, _ => RemoveBuff(buff));
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_trail);
            _trail = null;
            (buff.ParentScript as Spells.FioraQ)?.OnDashBuffRemoved(buff);
        }
    }
}
