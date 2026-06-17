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
            PersistsThroughDeath = true,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            BuffType = BuffType.KNOCKUP,
            IsHidden = false,
        };

        public StatsModifier StatsModifier { get; private set; }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            float desiredDuration = 1.2f;//1.2f
            float desiredHeight = 8.0f;
            // In-place knockup = BBMove with gravity (no BBKnockup): tiny +2u nudge, arc from gravity.
            ForceMove(unit, new Vector2(unit.Position.X + 2.0f, unit.Position.Y),
                2.0f / desiredDuration, gravity: desiredHeight / (desiredDuration * desiredDuration),
                facing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING);

        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}
