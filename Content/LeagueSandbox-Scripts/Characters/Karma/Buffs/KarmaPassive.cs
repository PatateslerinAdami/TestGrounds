using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;


namespace Buffs
{
    internal class KarmaPassive: IBuffGameScript {
        private ObjAIBase _karma;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _karma = ownerSpell.CastInfo.Owner;
            ApiEventManager.OnDealDamage.AddListener(this, _karma, OnDealDamage);
        }

        private void OnSpellHit(Spell spell) {
            var reductionAmount = 1f + 0.5f * (_karma.Spells[3].CastInfo.SpellLevel -1);
            _karma.Spells[3].LowerCooldown(reductionAmount);
        }

        private void OnDealDamage(DamageData data) {
            if (!IsValidTarget(_karma, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)) return;
            if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
            var reductionAmount = 0.5f + 0.25f * (_karma.Spells[3].CastInfo.SpellLevel -1);
            _karma.Spells[3].LowerCooldown(reductionAmount);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}