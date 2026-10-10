using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoThrustVisuals : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff;
        private Spell _spell;
        private Particle _wind, _hand;
        private bool _completed;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            _spell = ownerSpell;
            _completed = false;
            var owner = ownerSpell.CastInfo.Owner;
            _hand = AddParticle(owner, owner, "Yasuo_Q_Hand", owner.Position, lifetime: buff.Duration, bone: "R_Hand");
            if (ownerSpell.SpellName != "YasuoQ3")
            {
                string name = ownerSpell.SpellName == "YasuoQ2" ? "Yasuo_Base_Q_windstrike_02" : "Yasuo_Base_Q_WindStrike";
                _wind = AddParticleTarget(owner, owner, name, owner, lifetime: buff.Duration,
                    direction: Spells.YasuoCastHelpers.ThrustDirection(ownerSpell), flags: FXFlags.GivenDirection | FXFlags.BindDirection);
            }

            ApiEventManager.OnSpellPostCast.AddListener(this, ownerSpell, OnComplete);
            ApiEventManager.OnDeath.AddListener(this, owner, OnDeath);
        }

        private void OnComplete(Spell spell)
        {
            _completed = true;
        }

        private void OnDeath(DeathData data)
        {
            RemoveBuff(_buff);
        }

        public void OnUpdate(float diff)
        {
            if (!_completed && _spell.State != SpellState.STATE_CASTING)
            {
                RemoveBuff(_buff);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_wind);
            RemoveParticle(_hand);
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
