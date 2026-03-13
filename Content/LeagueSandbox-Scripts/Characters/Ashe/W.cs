using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class Volley : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        private ObjAIBase _owner;
        public static List<AttackableUnit> HitTargets = new List<AttackableUnit>();
        bool isEvolved = false;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            HitTargets.Clear();
        }

        public void OnSpellPostCast(Spell spell)
        {
            Vector2 start = new Vector2(spell.CastInfo.SpellCastLaunchPosition.X, spell.CastInfo.SpellCastLaunchPosition.Z);
            Vector2 end = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
            Vector2 direction = Vector2.Normalize(end - start);

            int arrowCount = 9;
            float totalSpreadDegrees = 40f;
            if(isEvolved)
            {
                arrowCount = 36;
                totalSpreadDegrees = 360f;
            }

            FireVolley(start, direction, arrowCount, totalSpreadDegrees);
        }

        private void FireVolley(Vector2 start, Vector2 direction, int arrowCount, float totalSpreadDegrees)
        {
            if (arrowCount <= 0) return;

            if (arrowCount == 1)
            {
                Vector2 missileEnd = start + (direction * 1200f);
                CreateCustomMissile(_owner, "VolleyAttack", start, missileEnd, new MissileParameters { Type = MissileType.Circle });
                return;
            }

            float startAngle = -totalSpreadDegrees / 2f;
            float angleStep = totalSpreadDegrees / (arrowCount - 1);

            for (int i = 0; i < arrowCount; i++)
            {
                float currentAngle = startAngle + (i * angleStep);
                Vector2 rotatedDir = RotateVector(direction, currentAngle);

                Vector2 missileEnd = start + (rotatedDir * 1200f);

                var parameters = new MissileParameters { Type = MissileType.Circle };
                CreateCustomMissile(_owner, "VolleyAttack", start, missileEnd, parameters);
            }
        }

        private Vector2 RotateVector(Vector2 v, float degrees)
        {
            float radians = degrees * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }
        public void OnSpellEvolve(Spell spell)
        {
            isEvolved = true;
        }
    }

    public class VolleyAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters { Type = MissileType.Circle }
        };

        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            spell.SpellData.LineWidth = 40f;
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (missile != null)
            {
                if (!Volley.HitTargets.Contains(target))
                {
                    Volley.HitTargets.Add(target);

                    byte level = _owner.GetSpell("Volley").CastInfo.SpellLevel;
                    float damage = 5f + (15f * level) + _owner.Stats.AttackDamage.Total;

                    target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
                    var Slow = new Slow()
                    {
                        SlowPercent = 0.5f
                    };
                    AddBuff(Slow, "Slow", 2.0f, 1, spell, target, _owner);
                }

                AddParticle(_owner, target, "ashe_base_w_tar.troy", default);
                missile.SetToRemove();
            }
        }
    }
}