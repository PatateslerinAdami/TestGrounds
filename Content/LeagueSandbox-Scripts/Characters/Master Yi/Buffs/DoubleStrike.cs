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
    internal class DoubleStrike : IBuffGameScript
    {

        private ObjAIBase _masterYi;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };

        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _masterYi = buff.SourceUnit;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            DamageType damageType = buff.BuffVars.Get("damageType", DamageType.DAMAGE_TYPE_PHYSICAL);
            DamageResultType damageResultType = buff.BuffVars.Get("damageResultType", DamageResultType.RESULT_NORMAL);
            AttackableUnit target = buff.BuffVars.Get<AttackableUnit>("target");
            if (!target.IsDead)
            {
                target.TakeDamage(_masterYi, buff.BuffVars.GetFloat("damage", 0f) * 0.5f, damageType,
                    DamageSource.DAMAGE_SOURCE_ATTACK, damageResultType);
            }
            _masterYi.ResetAutoAttackSpell();
        }
    }
}