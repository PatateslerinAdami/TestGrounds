using System;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells
{
    internal static class EzrealHelpers
    {
        public static Vector2 Aim(Spell spell)
        {
            return new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        }

        public static void CreateMysticShotFan(Spell source, params float[] angles)
        {
            var owner = source.CastInfo.Owner;
            var origin = owner.Position;
            var direction = Aim(source) - origin;
            if (direction.LengthSquared() < 0.001f)
            {
                direction = new Vector2(owner.Direction.X, owner.Direction.Z);
            }

            if (direction.LengthSquared() < 0.001f)
            {
                direction = Vector2.UnitX;
            }

            direction = Vector2.Normalize(direction);
            var carrierName = owner.SkinID == 5
                ? "EzrealMysticShotPulseMissile"
                : "EzrealMysticShotMissile";
            var carrier = owner.GetSpell(carrierName);
            carrier.CastInfo.SpellLevel = source.CastInfo.SpellLevel;
            var parameters = new MissileParameters
            {
                Type = MissileType.Circle
            };

            foreach (float angle in angles)
            {
                var rotatedDirection = Vector2.Transform(direction, Matrix3x2.CreateRotation(angle));
                var end = origin + rotatedDirection * carrier.GetCurrentCastRange();
                ApiFunctionManager.CreateCustomMissile(owner, carrierName, origin, end, parameters, customHeightOffset: 150f);
            }
        }

        public static void OnHit(SpellMissile missile, Action<AttackableUnit> hit)
        {
            if (missile == null)
            {
                return;
            }

            ApiEventManager.OnSpellMissileHit.AddListener(missile, missile, (contact, target) =>
            {
                hit(target);
            });
            ApiEventManager.OnSpellMissileEnd.AddListener(missile, missile, ended =>
            {
                ApiEventManager.RemoveAllListenersForOwner(missile);
            });
        }
    }
}
