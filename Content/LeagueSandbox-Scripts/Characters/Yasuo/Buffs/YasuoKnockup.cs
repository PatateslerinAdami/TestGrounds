using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoQ3Mis : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            BuffType = BuffType.KNOCKUP,
            IsHidden = false,
        };

        public StatsModifier StatsModifier { get; private set; }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            float desiredDuration = 1.2f;//1.2f
            float desiredHeight = 8.0f;
            Vector2 startPosition = unit.Position;
            Vector2 endPosition = new Vector2(startPosition.X + 2.0f, startPosition.Y);// 2f
            float horizontalDistance = Vector2.Distance(startPosition, endPosition);
            float requiredSpeed = horizontalDistance / desiredDuration;
            float requiredGravity = desiredHeight / (desiredDuration * desiredDuration);

            ForceMovement(
            unit,
            "RUN",
            endPosition,
            requiredSpeed,
            0,
            requiredGravity,
            0,
            movementOrdersFacing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING
            );

        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}
