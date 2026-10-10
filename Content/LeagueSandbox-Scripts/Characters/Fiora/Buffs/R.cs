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
    public class FioraDance : IBuffGameScript
    {
        private Spells.FioraDance _controller;
        private Particle _casterEffect;

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
            _controller = buff.ParentScript as Spells.FioraDance;
            buff.SetStatusEffect(StatusFlags.Targetable | StatusFlags.CanMove | StatusFlags.CanAttack, false);
            unit.StopMovement();
            StopTargetingUnit(unit);
            ApiEventManager.OnCanCast.AddListener(this, unit, (_, cast) => cast == null || cast.CastInfo.SpellSlot >= 4);
            HideHealthBar(unit);
            _casterEffect = AddParticleTarget(buff.SourceUnit, unit, "Fiora_Dance_cas.troy", unit, lifetime: -1);
            ApiEventManager.OnDeath.AddListener(this, unit, _ => RemoveBuff(buff));
        }

        public void OnUpdate(float diff)
        {
            _controller?.AdvanceSequence(diff);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_casterEffect);
            _casterEffect = null;
            HideHealthBar(unit, hide: false);
            _controller?.OnSequenceRemoved(buff);
        }
    }

    public class FioraDanceGhosted : IBuffGameScript
    {
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
        }
    }
}
