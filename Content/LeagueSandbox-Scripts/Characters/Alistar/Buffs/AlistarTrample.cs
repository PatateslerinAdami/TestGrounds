using System.Collections.Generic;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Enums;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Buffs
{
    internal class AlistarTrample : IBuffGameScript
    {
        private ObjAIBase _alistar;
        private Particle _p1, _p2, _p3, _p4, _p5, _p6;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsDeathRecapSource = true
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _alistar = buff.SourceUnit;
            SetStatus(_alistar, StatusFlags.Ghosted, true);
            _alistar.SetAnimStates(new Dictionary<string, string>
            {
                { "Idle1", "TRAMPLE" },
                { "run", "Run2" }
            });
            _p1 = SpellEffectCreate("alistar_trample_01", _alistar, _alistar, _alistar, lifetime: buff.Duration, fowVisibilityRadius: 10f);
            _p2 = SpellEffectCreate("alistar_trample_hand", _alistar, _alistar, _alistar, lifetime: buff.Duration,
                boneName: "L_hand", fowVisibilityRadius: 10f);
            _p3 = SpellEffectCreate("alistar_trample_hand", _alistar, _alistar, _alistar, lifetime: buff.Duration,
                boneName: "R_hand", fowVisibilityRadius: 10f);
            _p4 = SpellEffectCreate("alistar_trample_head", _alistar, _alistar, _alistar, lifetime: buff.Duration,
                boneName: "C_Buffbone_Glb_Head_Loc", fowVisibilityRadius: 10f);
            _p5 = SpellEffectCreate("alistar_nose_puffs", _alistar, _alistar, _alistar, lifetime: buff.Duration,
                boneName: "BUFFBONE_CSTM_NOSE2", flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
            _p6 = SpellEffectCreate("alistar_nose_puffs", _alistar, _alistar, _alistar, lifetime: buff.Duration,
                boneName: "BUFFBONE_CSTM_NOSE1", flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
        }

        public void OnUpdate(Buff buff, float diff)
        {
            // Riot BBExecutePeriodically (S1 AlistarTrample): TimeBetweenExecutions=1 -> 1s cadence,
            // drift-free, anchor stored in this buff instance's BuffVars (= Riot TrackTimeVarTable).
            // ExecuteImmediately=true fires on the first update. No step cap: the buff duration gates
            // it (OnUpdate stops once the buff ends). Range query includes AffectTurrets so enemy
            // structures are hit; the double-damage test (AffectEnemies|AffectMinions) matches only
            // enemy minions, per S1's PercentOfAttack=2 minion branch. Damage base uses the current
            // patch's 6+level curve (the S1 10..23 rank table was changed in a later patch).
            ExecutePeriodically(buff.BuffVars, "trampleTick", 1000f, executeImmediately: true, 0, () =>
            {
                var enemiesInRange = GetUnitsInRange(_alistar, _alistar.Position, 300f, true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                    SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral |
                    SpellDataFlags.AffectBuildings | SpellDataFlags.AffectTurrets);
                foreach (var enemy in enemiesInRange)
                {
                    var dmg = 7f + 1f * (_alistar.Stats.Level - 1) + _alistar.Stats.AbilityPower.Total * 0.1f;
                    enemy.TakeDamage(_alistar,
                        IsValidTarget(_alistar, enemy, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions)
                            ? dmg * 2f
                            : dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                        DamageResultType.RESULT_NORMAL);
                }
            });
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_p1);
            RemoveParticle(_p2);
            RemoveParticle(_p3);
            RemoveParticle(_p4);
            RemoveParticle(_p5);
            RemoveParticle(_p6);
            _alistar.SetAnimStates(new Dictionary<string, string>
            {
                { "Idle1", "" },
                { "run", "" }
            });
            SetStatus(_alistar, StatusFlags.Ghosted, false);
        }
    }
}