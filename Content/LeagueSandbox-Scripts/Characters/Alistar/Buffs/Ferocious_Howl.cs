using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Buffs
{
    internal class Ferocious_Howl : IBuffGameScript {
        private ObjAIBase _alistar;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.HEAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _alistar = buff.SourceUnit;
            var buffs = _alistar.GetBuffs();
            foreach (var buff1 in buffs.Where(buff1 => buff1.BuffType is BuffType.BLIND 
                                                                         or BuffType.KNOCKBACK 
                                                                         or BuffType.KNOCKUP 
                                                                         or BuffType.CHARM 
                                                                         or BuffType.COMBAT_DEHANCER 
                                                                         or BuffType.DISARM 
                                                                         or BuffType.FEAR 
                                                                         or BuffType.FLEE 
                                                                         or BuffType.NEAR_SIGHT 
                                                                         or BuffType.POLYMORPH 
                                                                         or BuffType.POISON 
                                                                         or BuffType.SHRED 
                                                                         or BuffType.SILENCE 
                                                                         or BuffType.STUN 
                                                                         or BuffType.SNARE 
                                                                         or BuffType.SLEEP 
                                                                         or BuffType.SUPPRESSION 
                                                                         or BuffType.TAUNT 
                                                                         or BuffType.SLOW)) { RemoveBuff(buff1); }
            AddParticleTarget(_alistar, unit, "minatuar_unbreakableWill_cas", unit, buff.Duration);
            AddParticleTarget(_alistar, unit, "feroscioushowl_cas2", unit, buff.Duration);
            ApiEventManager.OnPreTakeDamage.AddListener(this, _alistar, OnPreTakeDamage);
            StatsModifier.AttackDamage.FlatBonus = 60f + 15f * (ownerSpell.CastInfo.SpellLevel - 1);
            unit.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }

        private void OnPreTakeDamage(DamageData data) {
            if (data.DamageType is DamageType.DAMAGE_TYPE_MAGICAL or DamageType.DAMAGE_TYPE_PHYSICAL) {
                data.PostMitigationDamage = data.PostMitigationDamage * 0.3f;
            }
        }
    }
}