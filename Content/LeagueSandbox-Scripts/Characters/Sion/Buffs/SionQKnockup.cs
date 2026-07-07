using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
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
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs
{
    // Sion Q slam CC. Wire (docs/SIONQ_WIRE_EXTRACTION.md): one visible KNOCKUP buff whose
    // duration is the TOTAL stun (1.25-2.25s); the physical knockup arc covers the first
    // 0.5-1.0s ("KnockupTime" buff variable), and when the target LANDS a separate visible
    // "Stun" buff (with LOC_Stun FX, global Stun script) covers the remainder ("StunTail").
    // Riot architecture pattern (see Alistar PulverizeSpeed): the follow-up stun is applied
    // exactly once from the forced-movement END event; the engine's normal tenacity reduction
    // applies to that Stun at add time (KNOCKUP itself is not tenacity-reducible).
    public class SionQKnockUp : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private Spell _spell;
        private float _stunTail;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            // KNOCKUP has no BuffType-derived capability disable — hold the unit manually
            // for the buff's lifetime (= total stun duration).
            unit.SetStatus(StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast, false);

            _spell = ownerSpell;
            float knockupTime = buff.BuffVars.GetFloat("KnockupTime", 0.5f);
            _stunTail = buff.BuffVars.GetFloat("StunTail", 0.75f);

            ApiEventManager.OnMoveEnd.AddListener(this, unit, OnMoveEnd);

            // Wire-exact (forcemovedump on the test replay): Riot's in-place knockup is a ~10u
            // path with CONSTANT ParabolicGravity=20 and PathSpeedOverride = pathLen/knockupTime
            // (observed: speed 16.4 @ 0.61s knockup, 14.0 @ 0.75s — both = ~10/duration, g=20).
            const float knockPath = 10f;
            const float knockGravity = 20f;
            var bouncePos = GetRandomPointInAreaUnit(unit, 10, 10f);
            ForceMove(unit, bouncePos,
                knockPath / knockupTime, gravity: knockGravity,
                facing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
                movementName: "SionQKnockUp");
        }

        private void OnMoveEnd(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters.MovementName != "SionQKnockUp")
            {
                return;
            }
            // Landing: remaining CC becomes a visible Stun (global script adds LOC_Stun.troy),
            // matching the wire's BuffAdd2 [Stun] + LOC_Stun at +knockup-time. Added normally —
            // the engine applies tenacity to it here, once.
            if (!unit.IsDead && _stunTail > 0f)
            {
                ApiFunctionManager.AddBuff("Stun", _stunTail, 1, _spell, unit, _spell.CastInfo.Owner);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            unit.SetStatus(StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast, true);
        }

        public void OnUpdate(float diff)
        {
        }
    }

    // Flail slow: -50% MS. Wire: SLOW type, visible, 0.3s (duration set by the caller).
    public class SionQSlow : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
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

    // Charge-tracking buff on Sion during the 2s charge. Wire: COMBAT_ENCHANCER, hidden, dur 2.0.
    public class SionQ : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }

    // Sound-event carrier buffs (client plays material hit sounds off these). Wire: AURA,
    // hidden, 0.25s. Caster gets SionQSound{Before,After}Half; hit targets get the *Hit
    // variants — slam applies BOTH hit variants, flail only BeforeHalfHit.
    public class SionQSoundBeforeHalf : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }

    public class SionQSoundAfterHalf : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }

    public class SionQSoundBeforeHalfHit : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }

    public class SionQSoundAfterHalfHit : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }
}
