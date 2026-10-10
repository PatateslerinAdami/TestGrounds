using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FioraRiposte : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true
        };

        public void OnSpellPostCast(Spell spell)
        {
            var variables = new BuffVariables();
            variables.Set("Rank", spell.CastInfo.SpellLevel);
            variables.Set("Damage",
                spell.SpellData.GetEffectAmount(3, spell.CastInfo.SpellLevel) + spell.CastInfo.Owner.Stats.AbilityPower.Total);
            AddBuff("FioraRiposte", spell.SpellData.GetEffectAmount(2, spell.CastInfo.SpellLevel), 1, spell, spell.CastInfo.Owner,
                spell.CastInfo.Owner, buffVariables: variables);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            owner.RemoveBuffsWithName("FioraRiposte");
        }
    }

    public class FioraRiposteMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true
        };

        private readonly Dictionary<SpellMissile, float> _damage = new Dictionary<SpellMissile, float>();
        private Spell _spell;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _spell = spell;
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnHit);
        }

        public void Reflect(AttackableUnit target, float damage, byte rank)
        {
            if (target == null)
            {
                return;
            }

            if (target.IsDead)
            {
                return;
            }

            if (target.IsToRemove())
            {
                return;
            }

            if (_spell.CastInfo.Owner.IsToRemove())
            {
                return;
            }

            var owner = _spell.CastInfo.Owner;
            _spell.CastInfo.SpellLevel = rank;
            var missile = _spell.CreateCustomMissile(owner.Position, target.Position,
                new MissileParameters { Type = MissileType.Target, ReplicationHeightOffset = 100, ReplicationEndHeightOffset = 0 },
                isForceCastingOrChannel: false, isOverrideCastPosition: false, customHeightOffset: 0, target: target);
            if (missile == null)
            {
                return;
            }

            if (owner.IsToRemove())
            {
                missile.SetToRemove();
                return;
            }

            missile.SetSpeed(_spell.SpellData.MissileSpeed);
            _damage[missile] = damage;
        }

        private void OnHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (missile == null || !_damage.Remove(missile, out float damage))
            {
                return;
            }

            missile.SetToRemove();
            if (target == null)
            {
                return;
            }

            if (target.IsDead)
            {
                return;
            }

            if (target.IsToRemove())
            {
                return;
            }

            if (!target.GetIsTargetableToTeam(spell.CastInfo.Owner.Team))
            {
                return;
            }

            target.TakeDamage(spell.CastInfo.Owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
        }

        public void OnUpdate(float diff)
        {
            foreach (var missile in _damage.Keys.Where(m => m.IsToRemove()).ToArray())
            {
                _damage.Remove(missile);
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            foreach (var missile in _damage.Keys)
            {
                missile.SetToRemove();
            }

            _damage.Clear();
        }
    }

    public class FioraRiposteMissileTower : FioraRiposteMissile
    {
    }

    public class FioraRiposteMissileTowerChs : FioraRiposteMissile
    {
    }

    public class FioraRiposteMissileTowerDom : FioraRiposteMissile
    {
    }
}
