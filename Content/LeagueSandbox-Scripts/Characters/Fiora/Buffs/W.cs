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
    public class FioraRiposte : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Buff _buff;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            _buff = buff;
            ApiEventManager.OnPreHit.AddListener(this, unit, Parry);
            ApiEventManager.OnDeath.AddListener(this, unit, _ => RemoveBuff(buff));
        }

        private static bool Eligible(AttackableUnit attacker)
        {
            if (attacker is Champion or Monster)
            {
                return true;
            }

            return attacker is Minion && (attacker.CharData.UnitTags & (UnitTag.Minion_Lane_Siege | UnitTag.Minion_Lane_Super)) != 0;
        }

        private void Parry(DamageData data)
        {
            if (_buff.Remove)
            {
                return;
            }

            if (_owner.IsDead)
            {
                return;
            }

            if (data.IsHitBlocked)
            {
                return;
            }

            if (!data.IsAutoAttack)
            {
                return;
            }

            if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK)
            {
                return;
            }

            if (data.Damage <= 0)
            {
                return;
            }

            if (data.Attacker == null)
            {
                return;
            }

            if (data.Attacker.Team == _owner.Team)
            {
                return;
            }

            if (!Eligible(data.Attacker))
            {
                return;
            }

            float damage = _buff.Variables.GetFloat("Damage");
            byte rank = (byte)_buff.Variables.GetInt("Rank");
            data.BlockHit(DamageResultType.RESULT_INVULNERABLENOMESSAGE);
            RemoveBuff(_buff);
            if (_owner.IsDead || _owner.IsToRemove())
            {
                return;
            }

            NotifyNPCMessage(_owner, "game_lua_Riposte_Block");
            NotifyNPCMessage(data.Attacker, "game_lua_Riposte_Block");
            AddParticleTarget(_owner, _owner, "Fiora_Parry_block.troy", _owner, lifetime: 0.5f, flags: FXFlags.BindDirection);
            (_owner.GetSpell("FioraRiposteMissile")?.Script as Spells.FioraRiposteMissile)?.Reflect(data.Attacker, damage, rank);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
