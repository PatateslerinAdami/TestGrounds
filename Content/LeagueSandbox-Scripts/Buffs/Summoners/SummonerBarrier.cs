using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using GameServerCore.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using GameServerLib.GameObjects.AttackableUnits; 

namespace Buffs
{
    internal class SummonerBarrier : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SPELL_SHIELD,
            IsHidden = false,
            BuffAddType = BuffAddType.REPLACE_EXISTING, 
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        

        private Shield _barrierShield;
        private Buff _buff;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            var owner = ownerSpell.CastInfo.Owner;

            // 1. Afișăm particula aurie pe caracter
            AddParticleTarget(owner, unit, "Global_SS_Barrier", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC");

            // 2. Calculăm valoarea matematică a barierei
            float shieldAmount = 95f + (20f * unit.Stats.Level);

            // 3. Creăm scutul în memorie și îl atașăm de campion
            _barrierShield = new Shield((ObjAIBase)owner, unit, true, true, shieldAmount);
            unit.AddShield(_barrierShield);
        }


        public void OnUpdate(float diff)
        {
            if (_barrierShield != null && _barrierShield.IsConsumed())
            {
                _buff.DeactivateBuff();
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {

            if (_barrierShield != null)
            {
                unit.RemoveShield(_barrierShield);
            }
        }
    }
}