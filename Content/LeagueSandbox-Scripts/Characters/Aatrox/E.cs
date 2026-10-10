using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class AatroxE : ISpellScript
    {
        private ObjAIBase _owner;
        private Vector2 _direction;
        private float _damage, _slowDuration;
        private readonly Dictionary<SpellMissile, object> _listeners = new Dictionary<SpellMissile, object>();

        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            AatroxHelpers.PayHealth(owner, owner.Stats.CurrentHealth * 0.05f);
            _damage = 75 + 35 * (spell.CastInfo.SpellLevel - 1) + 0.6f * owner.Stats.AbilityPower.Total + 0.6f * (owner.Stats.AttackDamage.Total - owner.Stats.AttackDamage.BaseValue);
            _slowDuration = 1.75f + 0.25f * (spell.CastInfo.SpellLevel - 1);
            var direction = end - owner.Position;
            if (direction.LengthSquared() < 0.001f)
            {
                direction = new Vector2(owner.Direction.X, owner.Direction.Z);
            }

            _direction = direction.LengthSquared() < 0.001f ? Vector2.UnitX : Vector2.Normalize(direction);
            FaceDirection(owner.Position + _direction, owner, true);
        }

        public void OnSpellPostCast(Spell spell)
        {
            var hits = new HashSet<uint>();
            float damage = _damage, slow = _slowDuration;
            var start = _owner.Position;
            var aim = start + _direction * 1050;
            var side = new Vector2(-_direction.Y, _direction.X) * 115;
            Launch("AatroxEConeMissile2", start, aim, 100, hits, spell, damage, slow);
            foreach (var launch in new[]
            {
                start + side,
                start - side
            }

            )
            {
                Launch("AatroxEConeMissile", launch, launch + Vector2.Normalize(aim - launch) * 1075, 0, hits, spell, damage, slow);
            }
        }

        private void Launch(string name, Vector2 start, Vector2 end, float height, HashSet<uint> hits, Spell wrapper, float damage, float slow)
        {
            var carrier = _owner.GetSpell(name);
            carrier.CastInfo.SpellLevel = 0;
            var missile = carrier.CreateCustomMissile(start, end,
                new MissileParameters { Type = MissileType.Circle, ReplicationHeightOffset = height, ReplicationEndHeightOffset = height },
                isOverrideCastPosition: true, customHeightOffset: 0);
            if (missile == null)
            {
                return;
            }

            var listener = new object();
            _listeners.Add(missile, listener);
            ApiEventManager.OnSpellMissileHit.AddListener(listener, missile, (_, target) =>
            {
                if (target == null)
                {
                    return;
                }

                if (target.IsDead)
                {
                    return;
                }

                if (target.IsToRemove())
                {
                    return;
                }

                if (!IsValidTarget(_owner, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral))
                {
                    return;
                }

                if (!hits.Add(target.NetId))
                {
                    return;
                }

                AddParticleTarget(_owner, target, AatroxHelpers.Effect(_owner, "EMissile_Hit"), target, 0.5f);
                AddBuff("AatroxEConeMissile", slow, 1, wrapper, target, _owner);
                target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            });
            ApiEventManager.OnSpellMissileEnd.AddListener(listener, missile, _ =>
            {
                ApiEventManager.RemoveAllListenersForOwner(listener);
                _listeners.Remove(missile);
            },
                true);
        }

        private void OnUpdateStats(AttackableUnit unit, float diff)
        {
            SetSpellToolTipVar(_owner, 2, _owner.Stats.CurrentHealth * 0.05f, SpellbookType.SPELLBOOK_CHAMPION, 2,
                SpellSlotType.SpellSlots);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            foreach (var entry in _listeners)
            {
                ApiEventManager.RemoveAllListenersForOwner(entry.Value);
                entry.Key.SetToRemove();
            }

            _listeners.Clear();
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class AatroxEConeMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true
        };
    }

    public class AatroxEConeMissile2 : AatroxEConeMissile
    {
    }
}
