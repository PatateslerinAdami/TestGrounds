using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class EzrealRisingSpellForce : IBuffGameScript
    {
        private ObjAIBase _ezreal;
        private Particle _currentParticle;

        public BuffScriptMetaData BuffMetaData { get; set; } = new()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 5
        };

        public StatsModifier StatsModifier { get; } = new();
        public StatsModifier StatsModifier2 { get; } = new();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _ezreal = ownerSpell.CastInfo.Owner;

            unit.RemoveStatModifier(StatsModifier2);
            StatsModifier2.AttackSpeed.PercentBonus = 0.1f * buff.StackCount;
            unit.AddStatModifier(StatsModifier2);

            buff.SetToolTipVar(0, buff.StackCount * 10f);

            if (_currentParticle != null)
            {
                RemoveParticle(_currentParticle);
            }

            string particleName = buff.StackCount switch
            {
                1 => "Ezreal_glow1",
                2 => "Ezreal_glow2",
                3 => "Ezreal_glow3",
                4 => "Ezreal_glow4",
                _ => "Ezreal_glow5"
            };

            _currentParticle = AddParticle(_ezreal, _ezreal, particleName, _ezreal.Position, bone: "L_hand", lifetime: 2500000f);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_currentParticle != null)
            {
                RemoveParticle(_currentParticle);
            }
            unit.RemoveStatModifier(StatsModifier2);
        }
    }
}