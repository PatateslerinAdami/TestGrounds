using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs
{
    class KatarinaEReduction : IBuffGameScript
    {
        private ObjAIBase _katarina;
        private Particle _p;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _katarina = buff.SourceUnit;
            ApiEventManager.OnPreTakeDamage.AddListener(this, unit, OnPreTakeDamage);
            _p = _katarina.SkinID switch {
                9 => AddParticleTarget(unit, unit, "katarina_Skin09_E_buf", unit, 1.5f, flags: 0),
                _ => AddParticleTarget(unit, unit, "katarina_e_buf", unit, 1.5f, flags: 0)
            };
        }
        private void OnPreTakeDamage(DamageData data)
        {
            data.PostMitigationDamage *= 0.85f;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_p);
        }
    }
}
