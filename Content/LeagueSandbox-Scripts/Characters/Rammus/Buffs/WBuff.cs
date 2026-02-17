
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs
{
    internal class DefensiveBallCurl : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        ObjAIBase owner; Spell sp;
        Particle p;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            owner = ownerSpell.CastInfo.Owner;
            sp = ownerSpell;
            p = AddParticleTarget(owner, owner, "defensiveballcurl_buf", owner); p.isInfinite = true;
            owner.ChangeModelTo("RammusDBC");
            StatsModifier.Armor.PercentBonus = 1f;
            owner.AddStatModifier(StatsModifier);
            ApiEventManager.OnTakeDamage.AddListener(this, unit, OnTakeDamage, false);
        }
        public void OnTakeDamage(DamageData data)
        {
            if (data.IsAutoAttack)
            {
                var attacker = data.Attacker;
                p = AddParticleTarget(owner, attacker, "thornmail_tar.troy", attacker);
                attacker.TakeDamage(owner, 50f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, sp);
            }
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            owner.ChangeModelTo("Rammus");
            AddParticleTarget(owner, owner, "dbc_out", owner);
            p.SetToRemove();
            ownerSpell.SetCooldown(ownerSpell.GetCooldown(), true);
        }
    }
}
