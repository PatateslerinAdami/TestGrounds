using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class GuardianAngel : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INVULNERABILITY,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            buff.SetStatusEffect(StatusFlags.CanAttack, false);
            buff.SetStatusEffect(StatusFlags.CanMove, false);
            buff.SetStatusEffect(StatusFlags.CanCast, false);
            buff.SetStatusEffect(StatusFlags.Targetable, false);
            buff.SetStatusEffect(StatusFlags.Invulnerable, true);
            unit.StopMovement();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            if (unit.IsDead)
            {
                return;
            }

            unit.Stats.CurrentHealth = unit.Stats.HealthPoints.Total * 0.3f;
            unit.Stats.CurrentMana = unit.Stats.ManaPoints.Total * 0.3f;
        }
    }
}
