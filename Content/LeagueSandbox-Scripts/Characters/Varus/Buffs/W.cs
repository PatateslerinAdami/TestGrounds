using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class VarusWDebuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            IsHidden = false,
            MaxStacks = 3
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private Particle _particle;
        private Buff _buffRef;
        private int _currentStacks;
        private bool _isSubscribed = false;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buffRef = buff;
            _currentStacks = buff.StackCount;

            UpdateParticle(unit, ownerSpell.CastInfo.Owner);
            if (!_isSubscribed)
            {
                ApiEventManager.OnBeingSpellHit.AddListener(this, unit, OnBeingSpellHit, false);
                _isSubscribed = true;
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_particle);
            ApiEventManager.OnBeingSpellHit.RemoveListener(this, unit);
        }

        public void OnUpdate(float diff)
        {
            if (_buffRef != null)
            {
                if (_buffRef.StackCount != _currentStacks)
                {
                    _currentStacks = _buffRef.StackCount;
                    UpdateParticle(_buffRef.TargetUnit, _buffRef.OriginSpell.CastInfo.Owner);
                }
            }
        }

        private void OnBeingSpellHit(AttackableUnit unit, Spell spell, SpellMissile missile, SpellSector sector)
        {
            LoggerProvider.GetLogger().Warn($"HELLRRRO, {spell.CastInfo.SpellSlot}");
            if (spell.CastInfo.Owner == _buffRef.SourceUnit)
            {
                if (spell.CastInfo.SpellSlot == 45 || spell.CastInfo.SpellSlot == 2)
                {
                    float damagePerStack = unit.Stats.HealthPoints.Total * 0.05f;
                    float totalDamage = damagePerStack * _currentStacks;

                    unit.TakeDamage(_buffRef.SourceUnit, totalDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
                    unit.RemoveBuff(_buffRef);
                }
            }
        }

        private void UpdateParticle(AttackableUnit unit, GameObject caster)
        {
            RemoveParticle(_particle);
            switch (_currentStacks)
            {
                case 1:
                    _particle = AddParticle(caster, unit, "varusw_counter_01", unit.Position, lifetime: _buffRef.Duration);
                    break;
                case 2:
                    _particle = AddParticle(caster, unit, "varusw_counter_02", unit.Position, lifetime: _buffRef.Duration);
                    break;
                case 3:
                    _particle = AddParticle(caster, unit, "varusw_counter_03", unit.Position, lifetime: _buffRef.Duration);
                    _particle.isInfinite = true;
                    break;
            }
        }
    }
}