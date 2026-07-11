using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class Paranoia : IBuffGameScript
    {
        private readonly HashSet<AttackableUnit> _affectedUnits = new();

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            MaxStacks = 1,
            PersistsThroughDeath = true,
            IsNonDispellable = true
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }

        public void OnUpdate(Buff buff, float diff)
        {
            ExecutePeriodically(buff.SourceUnit.CharVars, "paranoiaTick", 1000f, false, 0, () =>
            {
                var unitsInRange = GetUnitsInRange(buff.SourceUnit, buff.SourceUnit.Position, 800f, true,
                        SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)
                    .ToHashSet();

                foreach (var unit in unitsInRange.Where(unit => _affectedUnits.Add(unit)))
                {
                    AddBuff("ParanoiaMissChance", 25000f, 1, buff.OriginSpell, unit, buff.SourceUnit,
                        infiniteduration: true);
                }

                foreach (var unit in _affectedUnits.Except(unitsInRange).ToList())
                {
                    RemoveBuff(unit, "ParanoiaMissChance");
                    _affectedUnits.Remove(unit);
                }
            });
        }
    }
}