using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;

namespace Spells
{
    public class TurretBasicAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            }
        };

        private ObjAIBase _owner;
        private int _consecutiveHits = 0;
        private float _timeSinceLastHit = 0f;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;

            var modifier = new StatsModifier();
            modifier.ArmorPenetration.PercentBaseBonus = 0.30f;
            _owner.AddStatModifier(modifier);

            ApiEventManager.OnHitUnit.AddListener(this, owner, OnHitUnit);
        }

        private void OnHitUnit(DamageData damageData)
        {
            if (damageData.Attacker != _owner)
            {
                return;
            }

            if (damageData.Target is Champion)
            {
                _consecutiveHits++;
                _timeSinceLastHit = 0f;

                float multiplier = 1.0f + Math.Min((_consecutiveHits - 1) * 0.375f, 0.75f);

                damageData.Damage *= multiplier;
                damageData.PostMitigationDamage = damageData.Target.Stats.GetPostMitigationDamage(damageData.Damage, DamageType.DAMAGE_TYPE_PHYSICAL, _owner);
            }
            else
            {
                _consecutiveHits = 0;
                _timeSinceLastHit = 0f;
            }
        }

        public void OnUpdate(float diff)
        {
            _timeSinceLastHit += diff / 1000f;

            if (_timeSinceLastHit > 3.5f)
            {
                _consecutiveHits = 0;
            }
        }
    }
}