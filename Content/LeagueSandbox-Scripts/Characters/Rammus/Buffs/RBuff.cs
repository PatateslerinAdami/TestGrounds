using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs
{
    internal class Tremors2 : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var owner = buff.SourceUnit;
            AddParticleTarget(owner, owner, "tremors_cas.troy", owner, lifetime: 8);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }

        public void OnUpdate(Buff buff, float diff)
        {
            ExecutePeriodically(buff.BuffVars, "termors2Ticker", 1000f, false, 0, () =>
            {
                Targets(buff.TargetUnit, buff.OriginSpell);
            });
        }

        private void Targets(AttackableUnit unit, Spell spell)
        {
            var enemiesInRange= GetUnitsInRange(unit, unit.Position, 500f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectTurrets);
            foreach (var enemy in enemiesInRange)
            {
                switch (enemy)
                {
                    case Champion champion:
                        champion.TakeDamage(unit, 50f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                            false, spell);
                        break;
                    case Minion minion:
                        minion.TakeDamage(unit, 5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                            false, spell);
                        break;
                    case LaneTurret turret:
                        turret.TakeDamage(unit, 500f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                            false, spell);
                        break;
                }
            }
        }
    }
}