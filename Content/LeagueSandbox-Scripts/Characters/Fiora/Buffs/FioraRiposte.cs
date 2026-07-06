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
    internal class FioraRiposte : IBuffGameScript
    {
        private Buff _buff;
        private ObjAIBase _fiora;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            PersistsThroughDeath = true,
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            _fiora = ownerSpell.CastInfo.Owner;
            ApiEventManager.OnPreTakeDamage.AddListener(this, _fiora, OnPreTakeDamage);
        }
        private void OnPreTakeDamage(DamageData damageData)
        {
            if (damageData.DamageSource is not DamageSource.DAMAGE_SOURCE_ATTACK) return;
            //_fiora.CancelAutoAttack(true);
            damageData.PostMitigationDamage = 0;
            SpellCast(_fiora, 4, SpellSlotType.ExtraSlots, true, damageData.Attacker, Vector2.Zero);
            RemoveBuff(_fiora, "FioraRiposteBuff");
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (buff.TimeElapsed >= buff.Duration)
            {
                ApiEventManager.OnPreTakeDamage.RemoveListener(this, _fiora, OnPreTakeDamage);
            }
        }
    }
}
