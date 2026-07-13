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
    internal class MasterYiPassive : IBuffGameScript
    {
        private ObjAIBase _masterYi;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true,
            PersistsThroughDeath = true,
            IsNonDispellable = true
        };

        public StatsModifier StatsModifier { get; private set; }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _masterYi = buff.SourceUnit;
            ApiEventManager.OnHitUnit.AddListener(this, _masterYi, OnHit);
        }
        
        private void OnHit(DamageData data)
        {
            // Count only genuine basic attacks — an on-hit spell like Alpha Strike deals
            // DAMAGE_SOURCE_ATTACK (so it procs on-hit effects) but must NOT advance Double Strike.
            if (!data.IsAutoAttack) return;
            if (_masterYi.HasBuff("DoubleStrikeReady") || _masterYi.HasBuff("DoubleStrike"))return;
            AddBuff("DoubleStrikeStacks", 4f, 1, _masterYi.AutoAttackSpell, _masterYi, _masterYi);
            if (_masterYi.GetBuffsWithName("DoubleStrikeStacks").Count != 3) return;
            AddBuff("DoubleStrikeReady", 4f, 1, _masterYi.AutoAttackSpell, _masterYi, _masterYi);
        }
    }
}