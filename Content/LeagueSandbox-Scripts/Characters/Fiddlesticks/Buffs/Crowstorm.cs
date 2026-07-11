using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class Crowstorm : IBuffGameScript
    {
        private Particle _crowstormParticle;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var owner = buff.SourceUnit;
            _crowstormParticle = SpellEffectCreate("crowstorm_green_cas.troy",owner, owner,  default, lifetime: 5f, effectNameForEnemy: "crowstorm_red_cas.troy", flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 500f);
        }

        public void OnUpdate(Buff buff, float diff)
        {
            ExecutePeriodically(buff.BuffVars, "crowstormTick",500f, false,0, () =>
            {
                var units = ForEachUnitInTargetArea(buff.SourceUnit, buff.SourceUnit.Position, 600f, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
                foreach (var unit in units)
                {
                    buff.OriginSpell.ApplyEffects(unit);
                }
            });
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_crowstormParticle);
        }
    }
}