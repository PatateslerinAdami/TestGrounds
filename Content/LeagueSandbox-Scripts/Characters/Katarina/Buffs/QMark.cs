using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class KatarinaQMark : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private Particle _markParticle;
        private Buff _buff;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            _markParticle = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "katarina_daggered.troy", unit, buff.Duration);
            ApiEventManager.OnBeingHit.AddListener(this, unit, OnAutoAttackHit, false);
            ApiEventManager.OnBeingSpellHit.AddListener(this, unit, OnSpellHit, false);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_markParticle);
            ApiEventManager.OnBeingHit.RemoveListener(this);
            ApiEventManager.OnBeingSpellHit.RemoveListener(this);
        }

        public void OnAutoAttackHit(AttackableUnit unit, AttackableUnit attacker)
        {
            if (attacker == _buff.SourceUnit)
            {
                ConsumeMark(unit);
            }
        }
        public void OnSpellHit(AttackableUnit unit, Spell spell, SpellMissile missile, SpellSector sector)
        {
            if (spell.CastInfo.Owner == _buff.SourceUnit)
            {
                string spellName = spell.SpellName;
                if (spellName == "KatarinaQ" || spellName == "KatarinaQMis")
                {
                    return;
                }
                ConsumeMark(unit);
            }
        }

        private void ConsumeMark(AttackableUnit target)
        {
            var owner = _buff.SourceUnit as ObjAIBase;
            if (owner != null)
            {
                int spellLevel = _buff.OriginSpell.CastInfo.SpellLevel;
                float baseDamage = 15 * spellLevel;
                float apRatio = 0.15f;
                float damage = baseDamage + (owner.Stats.AbilityPower.Total * apRatio);

                target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
                AddParticleTarget(owner, target, "katarina_enhanced2.troy", target, 1.0f);
            }

            _buff.DeactivateBuff();
        }
    }
}