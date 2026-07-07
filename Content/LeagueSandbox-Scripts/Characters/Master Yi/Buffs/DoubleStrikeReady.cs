using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameMaths;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs
{
    internal class DoubleStrikeReady : IBuffGameScript
    {

        private ObjAIBase _masterYi;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };

        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _masterYi = ownerSpell.CastInfo.Owner;
            _masterYi.SetAutoAttackSpell("MasterYiDoubleStrike", false);
            ApiEventManager.OnHitUnit.AddListener(this, _masterYi, OnHit);
        }

        private void OnHit(DamageData data)
        {
            // Only a genuine basic attack arms the second strike — NOT on-hit spells like Alpha Strike
            // (they deal DAMAGE_SOURCE_ATTACK but IsAutoAttack is now false for script-dealt damage).
            // OnHitUnit fires at damage application (missile arrival for ranged), so the timing is right.
            if (!data.IsAutoAttack) return;
            var variables = new VariableTable();
            variables.Set("damage", data.Damage);
            variables.Set("damageResultType", data.DamageResultType);
            variables.Set("damageType", data.DamageType);
            variables.Set("target", data.Target);
            AddBuff("DoubleStrike", 0.25f, 1, _masterYi.AutoAttackSpell, _masterYi, _masterYi, variableTable: variables);
            foreach (var buff in _masterYi.GetBuffsWithName("DoubleStrikeStacks"))
            {
                RemoveBuff(buff);
            }
            ApiEventManager.OnHitUnit.RemoveListener(this, _masterYi, OnHit);
            RemoveBuff(_masterYi,"DoubleStrikeReady");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}