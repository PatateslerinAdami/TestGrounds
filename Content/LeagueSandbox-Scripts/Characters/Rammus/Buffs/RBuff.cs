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
        float time;
        AttackableUnit au; Spell sp;
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            sp = ownerSpell;
            time = 800f;
            au = unit;
            var owner = ownerSpell.CastInfo.Owner;
            AddParticleTarget(owner, owner, "tremors_cas.troy", owner, lifetime: 8);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {

        }
        public void OnUpdate(float diff)
        {
            if (time >= 1000f)
            {
                time = 0f;
                Targets();
            }
            time += diff;
        }
        private void Targets()
        {
            var list = GetUnitsInRangeDiffTeam(au.Position, 500f, true, au);
            foreach (var unit in list)
            {
                switch (unit)
                {
                    case Champion champion:
                        champion.TakeDamage(au, 50f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, sp);
                        break;
                    case Minion minion:
                        minion.TakeDamage(au, 5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, sp);
                        break;
                    case LaneTurret turret:
                        turret.TakeDamage(au, 500f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, sp);
                        break;
                }
            }
        }
    }
}
