using System;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoPassiveMSShieldOn : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData { BuffType = BuffType.SPELL_SHIELD };
        public StatsModifier StatsModifier { get; set; } = new StatsModifier();
        
        private float _amt;
        private Shield _yasuoShield;
        private Buff _buff;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            float[] vals = { 100, 105, 110, 115, 120, 130, 140, 150, 165, 180, 200, 225, 255, 290, 330, 380, 440, 510 };
            _amt = vals[Math.Clamp(unit.Stats.Level - 1, 0, 17)];

            AddParticleTarget((ObjAIBase)unit, unit, "Yasuo_passive_activate", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC");
            AddParticleTarget((ObjAIBase)unit, unit, "Yasuo_Passive_Burst", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC");
            AddParticleTarget((ObjAIBase)unit, unit, "Yasuo_base_Passive_timer", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC");
            _yasuoShield = new Shield((ObjAIBase)unit, unit, true, true, _amt);
            unit.AddShield(_yasuoShield);
        }

        public void OnUpdate(float diff) 
        {
            if (_yasuoShield != null && _yasuoShield.IsConsumed()) 
            {
                _buff.DeactivateBuff();
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_yasuoShield != null)
            {
                unit.RemoveShield(_yasuoShield);
            }
        }
    }
}