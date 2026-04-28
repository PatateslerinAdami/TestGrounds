using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;


namespace Buffs
{
    internal class KarmaSpiritBind : IBuffGameScript {
        private       ObjAIBase      _karma;
        private       AttackableUnit _unit;
        private       Buff           _buff;
        private       Particle       _tetherParticle;
        private       bool           _isTetherBroken = false;
        private       bool           _isMantra       = false;
        private       float          _damageTimer    = 500f;
        private const float          MaxTicks        = 4f;
        private       float          _currentTickCount = 0f;
        private       Region         _bubble;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.HEAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _karma                               =  ownerSpell.CastInfo.Owner;
            _unit = unit;
            _buff = buff;
            _bubble = AddUnitPerceptionBubble(_unit, _unit.Stats.Size.Total, buff.Duration, _karma.Team, true, unit);
            
            _isMantra = buff.Variables.GetBool("isMantra");
            AddBuff("KarmaSpiritBindSlow", buff.Duration, 1, ownerSpell, unit, _karma);
            _tetherParticle = AddParticleTarget(_karma, _karma, _isMantra ? "Karma_Base_W_beam_R" : "Karma_Base_W_beam", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC", targetBone: "spine");
            if (!_isMantra) return;
            var healAmount = (_karma.Stats.HealthPoints.Total - _karma.Stats.CurrentHealth) * (0.2f + _karma.Stats.AbilityPower.Total * 0.0001f);
            _karma.TakeHeal(_karma, healAmount, HealType.SelfHeal);
        }

        public void OnUpdate(float diff) {
            LogInfo("" + Vector2.Distance(_karma.Position, _unit.Position));
            if (Vector2.Distance(_karma.Position, _unit.Position) >= 825f || _unit.IsDead) {
                _isTetherBroken = true;
                _buff.DeactivateBuff();
            }
            _damageTimer += diff;
            if (!(_damageTimer > 500f) || _currentTickCount >= MaxTicks) return;
            var bonusAp  = _karma.Stats.AbilityPower.Total * 0.6f;
            var bonusDmg = 75f + 75f * (_karma.GetSpell("KarmaMantra").CastInfo.SpellLevel - 1) + bonusAp;
            var ap       = _karma.Stats.AbilityPower.Total * 0.6f;
            var dmg      = 60f + 50f * (_karma.GetSpell("KarmaSpiritBind").CastInfo.SpellLevel - 1) + ap;
            if (_isMantra) {
                dmg += bonusDmg;
            }
            _unit.TakeDamage(_karma, dmg/MaxTicks, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                             DamageResultType.RESULT_NORMAL);
            var reductionAmount = 0.5f + 0.25f * (_karma.Spells[3].CastInfo.SpellLevel -1);
            _karma.Spells[3].LowerCooldown(reductionAmount);
            _currentTickCount++;
            _damageTimer = 0f;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_tetherParticle);
            _bubble.SetToRemove();
            RemoveBuff(unit, "KarmaSpiritBindSlow");
            if (_isTetherBroken) return;
            var rootDuration = buff.Variables.GetFloat("rootDuration");
            AddBuff("KarmaSpiritBindRoot", rootDuration, 1, ownerSpell, unit, _karma);
            if (!_isMantra) return;
            var healAmount = (_karma.Stats.HealthPoints.Total - _karma.Stats.CurrentHealth) * 0.25f;
            _karma.TakeHeal(_karma, healAmount, HealType.SelfHeal);
            
        }
    }
}