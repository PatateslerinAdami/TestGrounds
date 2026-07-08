using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    /// <summary>
    /// Twisted Treeline Shadow Altar team buff ("TwistedAura" master buff). Tiers escalate with how
    /// many altars the team controls (en_GB tooltips, authoritative; see project_tt_altars_420):
    ///   T0 (0 altars): +Bonus Mana Regeneration (0.67% of missing mana — wiki)
    ///   T1 (1 altar):  + +3 Gold per Kill
    ///   T2 (2 altars): + +10% bonus Attack Damage + +10% Ability Power
    /// Riot keeps the tiers as distinct buffs (T0 / T1Left / T1Right / T2) so the client shows the right
    /// icon/tooltip + TT_masterbuff troy. Effects are cumulative. Altars.cs swaps a champion's tier buff
    /// when its team's altar count changes. NonDispellable + PersistsThroughDeath (4.20 metadata).
    /// </summary>
    internal static class TreelineMasterBuffShared
    {
        // Bonus mana regen = 0.67% of MISSING mana. Tick period/exact formula is not wire-recoverable
        // (server-side); 0.67% per second of missing is a reasonable read of the wiki "0.67% per 1%
        // missing mana" — tune if needed.
        public const float ManaRegenPctMissingPerSec = 0.0067f;
        public const float GoldPerKill = 3.0f;

        public static void TickManaRegen(AttackableUnit unit, float diff)
        {
            if (unit?.Stats == null)
            {
                return;
            }
            float missing = unit.Stats.ManaPoints.Total - unit.Stats.CurrentMana;
            if (missing > 0.0f)
            {
                unit.Stats.CurrentMana += missing * ManaRegenPctMissingPerSec * (diff / 1000.0f);
            }
        }

        public static void GrantKillGold(DeathData deathData)
        {
            if (deathData?.Killer is Champion champ)
            {
                champ.AddGold(null, GoldPerKill, false);
            }
        }
    }

    // ---- T0: 0 altars — mana regen only ----
    internal class TreelineMasterBuffT0 : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            PersistsThroughDeath = true,
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private AttackableUnit _unit;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { _unit = unit; }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
        public void OnUpdate(Buff buff, float diff) { TreelineMasterBuffShared.TickManaRegen(_unit, diff); }
    }

    // ---- T1: 1 altar — mana regen + 3 gold/kill. Left/Right = which altar (effect identical). ----
    internal class TreelineMasterBuffT1Left : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            PersistsThroughDeath = true,
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private AttackableUnit _unit;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            ApiEventManager.OnKill.AddListener(this, unit, TreelineMasterBuffShared.GrantKillGold, false);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnKill.RemoveListener(this);
        }
        public void OnUpdate(Buff buff, float diff) { TreelineMasterBuffShared.TickManaRegen(_unit, diff); }
    }

    internal class TreelineMasterBuffT1Right : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            PersistsThroughDeath = true,
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private AttackableUnit _unit;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            ApiEventManager.OnKill.AddListener(this, unit, TreelineMasterBuffShared.GrantKillGold, false);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnKill.RemoveListener(this);
        }
        public void OnUpdate(Buff buff, float diff) { TreelineMasterBuffShared.TickManaRegen(_unit, diff); }
    }

    // ---- T2: 2 altars — mana regen + 3 gold/kill + 10% AD + 10% AP ----
    internal class TreelineMasterBuffT2 : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            PersistsThroughDeath = true,
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private AttackableUnit _unit;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            StatsModifier.AttackDamage.PercentBonus = 0.10f; // +10% bonus AD
            StatsModifier.AbilityPower.PercentBonus = 0.10f; // +10% AP
            unit.AddStatModifier(StatsModifier);
            ApiEventManager.OnKill.AddListener(this, unit, TreelineMasterBuffShared.GrantKillGold, false);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.RemoveStatModifier(StatsModifier);
            ApiEventManager.OnKill.RemoveListener(this);
        }
        public void OnUpdate(Buff buff, float diff) { TreelineMasterBuffShared.TickManaRegen(_unit, diff); }
    }
}
