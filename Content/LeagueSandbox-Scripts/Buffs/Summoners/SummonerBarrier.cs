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
            // NOT BuffType.SPELL_SHIELD: Barrier is a plain HP absorb. SPELL_SHIELD now drives the
            // engine spell-shield gate (AttackableUnit.ConsumeSpellShield) — mislabeling Barrier as
            // one would make it BLOCK an enemy ability outright instead of absorbing damage.
            BuffType = BuffType.INTERNAL,
            IsHidden = false,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            // 4.20 SummonerBarrier.lua stub: OnPreDamagePriority = 3 — barrier absorbs before
            // lower-priority shields on the same unit.
            OnPreDamagePriority = 3
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        

        private Shield _barrierShield;
        private Buff _buff;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;

            // 1. Afișăm particula aurie pe caracter
            AddParticleTarget(unit, unit, "Global_SS_Barrier", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC");

            // 2. Calculăm valoarea matematică a barierei
            float shieldAmount = 95f + (20f * unit.Stats.Level);

            // 3. Creăm scutul în memorie și îl atașăm de campion
            _barrierShield = new Shield((ObjAIBase)unit, unit, true, true, shieldAmount);
            unit.AddShield(_barrierShield);
        }


        public void OnUpdate(Buff buff, float diff)
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