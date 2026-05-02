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
    internal class YasuoDashWrapperChaos : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER, 
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false 
        };
        
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private Particle _timerParticle;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var owner = ownerSpell.CastInfo.Owner;
            int charLevel = owner.Stats.Level;
            int trueELevel = 1;
            if (charLevel >= 13) trueELevel = 5;
            else if (charLevel >= 12) trueELevel = 4;
            else if (charLevel >= 10) trueELevel = 3;
            else if (charLevel >= 8) trueELevel = 2;
            string animName = $"Yasuo_base_E_timer{trueELevel}.troy";
            _timerParticle = AddParticleTarget(owner, unit, animName, unit, buff.Duration);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_timerParticle != null)
            {
                RemoveParticle(_timerParticle);
            }
        }
    }
}