using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameMaths;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs
{
    internal class AbsoluteZero : IBuffGameScript
    {

        private ObjAIBase _nunu;
        private Spell _spell;
        private PeriodicTicker _periodicTicker;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };

        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _nunu = ownerSpell.CastInfo.Owner;
            _spell = ownerSpell;
        }

        // S1 BuffOnUpdateActions: every 0.25s re-apply the slow to enemies in range (refresh + catch units
        // that walk in during the channel). NO damage here — the damage is the spell's channel-stop burst
        // (R.cs OnSpellPostChannel/OnSpellChannelCancel -> Detonate), exactly like S1's ChannelingStop blocks.
        public void OnUpdate(Buff buff, float diff)
        {
            var ticks = _periodicTicker.ConsumeTicks(diff, 250f, false, 1, 10);
            if (ticks <= 0) return;
            var targets = GetUnitsInRange(_nunu, _nunu.Position, 575, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectHeroes);
            foreach (var target in targets)
            {
                AddBuff("AbsoluteZeroSlow", 3f, 1, _spell, target, _nunu);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}