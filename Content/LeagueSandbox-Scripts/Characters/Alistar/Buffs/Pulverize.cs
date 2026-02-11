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
namespace Buffs
{
    internal class Pulverize : IBuffGameScript
    {
        Particle stun;
        public float height;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false,
            MaxStacks = 1,
        };
        public float duration = -1f;
        public float heightD = -1f;
        public float goToLength = -1f;
        public StatsModifier StatsModifier { get; private set; }
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            stun = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "LOC_Airborne", unit, buff.Duration, bone: "head");

            float desiredDuration = 0.75f;//1.2f
            float desiredHeight = 5.0f;
            if (duration > 0)
            {
                desiredDuration = duration;
            }
            if (heightD > 0)
            {
                desiredHeight = heightD;
            }
            Vector2 startPosition = unit.Position;
            var dirGo = (unit.Position - ownerSpell.CastInfo.Owner.Position).Normalized();

            Vector2 endPosition = startPosition + dirGo * 100f;
            if (goToLength > 0)
            {
                endPosition = startPosition + dirGo * goToLength;
            }
            float horizontalDistance = Vector2.Distance(startPosition, endPosition);
            float requiredSpeed = horizontalDistance / desiredDuration;
            float requiredGravity = desiredHeight / (desiredDuration * desiredDuration);

            ForceMovement(unit, "RUN", endPosition, requiredSpeed, 0, requiredGravity, 0, movementOrdersFacing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING
            );
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(stun);
        }
    }
}
