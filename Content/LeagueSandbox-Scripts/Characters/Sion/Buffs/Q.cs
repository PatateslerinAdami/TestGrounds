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
            unit.SetStatus(StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast, false); // stun
            float knockupDuration = buff.Variables.GetFloat("KnockupTime", 0.5f);

            float desiredHeight = 8.0f;
            // In-place knockup = BBMove with gravity (no BBKnockup): tiny +2u nudge so the path is
            // nonzero, the arc comes from gravity = height/duration².
            ApiFunctionManager.ForceMove(unit, new Vector2(unit.Position.X + 2.0f, unit.Position.Y),
                2.0f / knockupDuration, gravity: desiredHeight / (knockupDuration * knockupDuration),
                facing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.SetStatus(StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast, true); // un-stun
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