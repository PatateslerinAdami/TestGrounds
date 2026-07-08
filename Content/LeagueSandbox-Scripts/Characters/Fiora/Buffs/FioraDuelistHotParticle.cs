using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using System.Collections.Generic;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using GameServerLib.GameObjects.AttackableUnits;
namespace Buffs
{
    internal class FioraDuelistHotParticle : IBuffGameScript
    {
        private Buff _buff;
        private Particle _heal;
        private ObjAIBase _fiora;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            PersistsThroughDeath = true,
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 4
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            RemoveParticle(_heal);
            _fiora = buff.SourceUnit;
            ApiEventManager.OnHitUnit.AddListener(this, _fiora, OnHitUnit, false);
            _heal = AddParticleTarget(_fiora, _fiora, "fiora_heal_buf", _fiora, 25000f, 1);
            switch (_buff.StackCount) { case 2: AddParticleTarget(_fiora, _fiora, "fiora_heal2_buf", _fiora); return; }
        }
        private void OnHitUnit(DamageData damageData)
        {
            _fiora.Stats.CurrentHealth += 7 + _fiora.Stats.Level;
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_heal);
        }
    }
}
