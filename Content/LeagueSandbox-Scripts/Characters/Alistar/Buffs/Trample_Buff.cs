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
    internal class Trample_Buff : IBuffGameScript {
        private       ObjAIBase _alistar;
        private       Buff      _buff;
        private const float     IntervalMs = 500.0f;
        private const int       MaxSteps   = 6;
        private       int       _stepsApplied;
        private       Particle  _p1, _p2, _p3, _p4, _p5, _p6;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _alistar = ownerSpell.CastInfo.Owner;
            _buff = buff;
            SetStatus(_alistar, StatusFlags.Ghosted, true);
            _alistar.SetAnimStates(new Dictionary<string, string> {
                { "Idle1", "Idle5" },
                { "run", "Run2" }
            });
            _p1 = AddParticleTarget(_alistar, _alistar, "alistar_trample_01",   _alistar, buff.Duration);
            _p2 = AddParticleTarget(_alistar, _alistar, "alistar_trample_hand", _alistar, buff.Duration, bone: "L_hand");
            _p3 = AddParticleTarget(_alistar, _alistar, "alistar_trample_hand", _alistar, buff.Duration, bone: "R_hand");
            _p4 = AddParticleTarget(_alistar, _alistar, "alistar_trample_head", _alistar, buff.Duration, bone: "head");
            _p5 = AddParticleTarget(_alistar, _alistar, "alistar_nose_puffs", _alistar, buff.Duration, bone: "BUFFBONE_CSTM_NOSE2");
            _p6 = AddParticleTarget(_alistar, _alistar, "alistar_nose_puffs", _alistar, buff.Duration, bone: "BUFFBONE_CSTM_NOSE1");
        }

        public void OnUpdate(float diff) {
            // Riot BBExecutePeriodically: 500ms cadence, drift-free, anchor stored in this buff's
            // BuffVars (= Riot TrackTimeVarTable). ExecuteImmediately mirrors the old _tickTime=500
            // init that fired on the first update. Capped at MaxSteps ticks over the buff's life.
            if (_stepsApplied >= MaxSteps) return;
            ExecutePeriodically(_buff.BuffVars, "trampleTick", IntervalMs, executeImmediately: true, () => {
                if (_stepsApplied >= MaxSteps) return;
                var dmg = 7f + 1f * (_alistar.Stats.Level - 1) + _alistar.Stats.AbilityPower.Total * 0.1f;
                var enemiesInRange = GetUnitsInRange(_alistar, _alistar.Position, 300f, true,
                                                     SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                                     SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectBuildings);
                foreach (var enemy in enemiesInRange) {
                    enemy.TakeDamage(_alistar, IsValidTarget(_alistar, enemy, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions) ? dmg * 2f : dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                                     DamageResultType.RESULT_NORMAL);
                }
                _stepsApplied++;
            });
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_p1);
            RemoveParticle(_p2);
            RemoveParticle(_p3);
            _alistar.SetAnimStates(new Dictionary<string, string> {
                { "Idle1", "" },
                { "run", "" }
            });
            SetStatus(_alistar, StatusFlags.Ghosted, false);
        }
    }
}