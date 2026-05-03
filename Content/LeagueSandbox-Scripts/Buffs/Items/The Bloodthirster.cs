using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
        public class BloodthirsterDummySpell : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING, // Asta dă reset timer-ului pe ecran!
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        
        private Particle _shieldParticle;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            // Particula Shield rămâne atașată cele 25 de secunde!
            _shieldParticle = AddParticleTarget(unit, unit, "Item_BTOverheal_Shield.troy", unit, buff.Duration, 1f, "C_BUFFBONE_GLB_CHEST_LOC");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            if (_shieldParticle != null)
            {
                RemoveParticle(_shieldParticle);
            }
        }

        public void OnUpdate(float diff) { }
    }
}