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
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class NunuQBuffLizard : IBuffGameScript
    {

        private ObjAIBase _nunu;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };
        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _nunu = ownerSpell.CastInfo.Owner;
            ApiEventManager.OnHitUnit.AddListener(this, _nunu, OnHit);

        }
        
        private void OnHit(DamageData data)
        {
            var dmg = _nunu.Stats.HealthPoints.Total * 0.01f;
            data.Target.TakeDamage(_nunu, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, DamageResultType.RESULT_NORMAL);
        }
        
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnHitUnit.RemoveListener(this, _nunu, OnHit);
        }
    }
}