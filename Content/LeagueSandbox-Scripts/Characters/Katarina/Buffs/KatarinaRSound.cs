using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Linq;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs;
    class KatarinaRSound : IBuffGameScript
    {
        private ObjAIBase _katarina;
        private Particle _p;
        private PeriodicTicker _periodicTicker;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _katarina = ownerSpell.CastInfo.Owner; 
            //PlaySound("KatarinaRSound",    _katarina);
            _p = _katarina.SkinID switch
            {
                9 => AddParticleTarget(_katarina, _katarina, "Katarina_Skin09_R_cas", _katarina, 2.5f),
                _ => AddParticleTarget(_katarina, _katarina, "Katarina_deathLotus_cas", _katarina, 2.5f)
            };
            switch (_katarina.SkinID) {
                default: PlayAnimation(unit, "Spell4");break;
                case 7:  PlayAnimation(unit, "Spell4", 0.2f); break;
            }
        }

        public void OnUpdate(float diff)
        {
            var ticks = _periodicTicker.ConsumeTicks(diff, 250f, false, 1, 10);
            if (ticks != 1) return;
            var closestUnits = GetUnitsInRange(_katarina, _katarina.Position, 550, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes).ToArray();
                
            var length = closestUnits.Length > 3 ? 3 : closestUnits.Length;
            for (var i = 0; i < length; i++)
            {
                SpellCast(_katarina, 0, SpellSlotType.ExtraSlots, true, closestUnits[i], _katarina.Position);
            }

        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_p);
            StopAnimation(unit, "Spell4");
        }
    }
