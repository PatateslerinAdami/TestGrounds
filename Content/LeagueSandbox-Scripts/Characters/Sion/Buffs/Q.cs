using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;

namespace Buffs
{
    public class SionQKnockup : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            float knockupDuration = buff.Variables.GetFloat("KnockupTime", 0.5f);

            float desiredHeight = 8.0f;
            Vector2 startPosition = unit.Position;
            Vector2 endPosition = new Vector2(startPosition.X + 2.0f, startPosition.Y);
            float horizontalDistance = Vector2.Distance(startPosition, endPosition);

            float requiredSpeed = horizontalDistance / knockupDuration;
            float requiredGravity = desiredHeight / (knockupDuration * knockupDuration);

            ApiFunctionManager.ForceMovement(
                unit,
                "RUN",
                endPosition,
                requiredSpeed,
                0,
                requiredGravity,
                0,
                consideredAsCC: true,
                movementOrdersFacing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING
            );
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.SetStatus(StatusFlags.Stunned, false);
        }

        public void OnUpdate(float diff)
        {
        }
    }
    public class SionQSlow : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            StatsModifier.MoveSpeed.PercentBonus = -0.5f;
            unit.AddStatModifier(StatsModifier);
        }
    }
    public class SionQ : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

    }
}