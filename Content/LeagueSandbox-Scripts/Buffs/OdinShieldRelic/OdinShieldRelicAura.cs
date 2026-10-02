using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Linq;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class OdinShieldRelicAura : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData();
        public StatsModifier StatsModifier { get; private set; }

        private bool _consumed;
        private Buff _buff;
        private Particle _particle;
        private AttackableUnit _unit;
        private float _timer = 250f;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            _unit = unit;
            _consumed = false;

            buff.SetStatusEffect(StatusFlags.Targetable, false);
            buff.SetStatusEffect(StatusFlags.Invulnerable, true);
            buff.SetStatusEffect(StatusFlags.ForceRenderParticles, true);
            buff.SetStatusEffect(StatusFlags.NoRender, true);
            _particle = AddParticleTarget(unit, unit, "odin_heal_rune", unit, -1f);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_particle);
            _particle = null;
            if (unit.IsToRemove()) return;

            unit.Stats.CurrentHealth = 0;
            unit.Die(new DeathData
            {
                Unit = unit,
                Killer = unit,
                DamageType = DamageType.DAMAGE_TYPE_TRUE,
                DamageSource = DamageSource.DAMAGE_SOURCE_INTERNALRAW
            });
        }

        public void OnUpdate(float diff)
        {
            if (_consumed || _unit == null || _unit.IsToRemove()) return;

            _timer += diff;
            if (_timer < 250f) return;
            _timer = 0;

            var champion = GetUnitsInRange(_unit, _unit.Position, 175f, true,
                SpellDataFlags.AffectHeroes | SpellDataFlags.AffectAllSides | SpellDataFlags.IgnoreClones)
                .OfType<Champion>().FirstOrDefault();
            if (champion == null) return;

            _consumed = true;
            AddBuff("OdinShieldRelicBuffHeal", 0.1f, 1, null, champion, _unit as ObjAIBase);
            _buff.DeactivateBuff();
        }
    }
}
