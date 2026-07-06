using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using System.Collections.Generic;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS
{
    public class CastInfo
    {
        public uint SpellHash { get; set; }
        public uint SpellNetID { get; set; }
        public byte SpellLevel { get; set; }
        public float AttackSpeedModifier { get; set; } = 1.0f;
        public ObjAIBase Owner { get; set; }
        // Ownership chain of the cast — NOT a sub-cast linking field. Replay-verified
        // (25 replays, 80,213 CastSpellAns): always == CasterNetID, except pet casts
        // (ShacoBoxSpell: caster = the box, chain owner = Shaco) where it points to the
        // owning champion — kill-credit/assist attribution. Use ResolveChainOwnerNetId.
        public uint SpellChainOwnerNetID { get; set; }
        public uint PackageHash { get; set; }
        public uint MissileNetID { get; set; }
        public Vector3 TargetPosition { get; set; }
        public Vector3 TargetPositionEnd { get; set; }
        // Raw aim/click position (Riot SpellCastInfo::CursorPos, 0x38) — the UNCLAMPED point the
        // player pointed at, kept distinct from TargetPosition (= Riot TargetPos, the position the
        // cast actually uses, which may be range-clamped or snapped onto a target unit). NOT on the
        // wire: the decomp's ToNetworkData serializes only TargetPos + TargetPosDragEnd, never
        // CursorPos, so this is server-internal. Riot uses it e.g. in PostponedSpell::Postpone
        // (AIBase.cpp:394, mTargetPos = ci.CursorPos) so a move-to-cast retry re-aims at the
        // original click, not the clamped point. Captured once at cast entry before TargetPosition
        // gets overwritten by fakePos/target-snap.
        public Vector3 CursorPos { get; set; }

        public List<CastTarget> Targets { get; set; }

        public float DesignerCastTime { get; set; }
        public float ExtraCastTime { get; set; }
        public float DesignerTotalTime { get; set; }
        public float Cooldown { get; set; }
        public float StartCastTime { get; set; }

        public bool IsAutoAttack { get; set; } = false;
        // Server-internal timing flags (NOT in the wire bitfield). Both mean "this cast
        // runs on the champion's attack timing" but enter through different pipelines:
        //  - UseAttackCastTime: attack-pipeline casts — set alongside IsAutoAttack (basic
        //    attacks + AA-overrides on slots 45-60) and by SpellCast(useAutoAttackSpell:
        //    true). Windup AND total come from the AS-scaled attack cycle (replay: tt =
        //    baseCycle/ASmod, ct/tt = champion castPercent — NDAttack ×56, JinxQAttack).
        //  - UseAttackCastDelay: NORMAL-pipeline spells whose data carries
        //    UseAutoattackCastTime (Yasuo Q+W, ItemTiamatCleave) or ConsideredAsAutoAttack
        //    (TF cards, Draven axes) — same attack-derived ct/tt (replay 630b7ceb: Yasuo
        //    QW at slot 0 sends ct=0.318/tt=1.448), but the cast still behaves like a
        //    regular spell cast (real windup state even when the data says InstantCast).
        public bool UseAttackCastTime { get; set; } = false;
        public bool UseAttackCastDelay { get; set; } = false;
        public bool IsSecondAutoAttack { get; set; } = false;
        public bool IsForceCastingOrChannel { get; set; } = false;
        public bool IsOverrideCastPosition { get; set; } = false;
        public bool IsClickCasted { get; set; } = false;

        public byte SpellSlot { get; set; }
        public float ManaCost { get; set; }
        public Vector3 SpellCastLaunchPosition { get; set; }
        public int AmmoUsed { get; set; }
        public float AmmoRechargeTime { get; set; }

        /// <summary>
        /// Per-cast script variable bag — the equivalent of Riot's SpellCastInfo::LuaVars
        /// ("InstanceVars"): every object carrying this CastInfo (missiles, sub-casts created
        /// with SpellCast(..., inheritVariablesFrom: ...)) shares ONE bag, so per-cast state
        /// flows between the parent spell script and its missile scripts without cross-script
        /// references. Clone() intentionally shares the reference (Riot shares one Lua table).
        /// Server-side only — Riot's LuaVarsLookup never appears on the wire.
        /// </summary>
        public BuffVariables Variables { get; set; } = new BuffVariables();

        /// <summary>
        /// Resolves the wire SpellChainOwnerNetID for a caster: pets report their owning
        /// champion (replay-verified via ShacoBoxSpell), everyone else reports themselves.
        /// </summary>
        public static uint ResolveChainOwnerNetId(ObjAIBase caster)
        {
            if (caster is Minion minion && minion.Owner != null)
            {
                return minion.Owner.NetId;
            }
            return caster != null ? caster.NetId : 0;
        }

        /// <summary>
        /// Adds the specified unit to the list of CastTargets.
        /// </summary>
        /// <param name="target">Unit to add.</param>
        public void AddTarget(AttackableUnit target)
        {
            Targets.Add(new CastTarget(target, CastTarget.GetHitResult(target, IsAutoAttack, Owner.IsNextAutoCrit, Owner.IsNextAutoMiss, Owner.IsNextAutoDodged)));
        }

        /// <summary>
        /// Removes the specified unit from the list of targets for this spell.
        /// </summary>
        /// <param name="target">Unit to remove.</param>
        public bool RemoveTarget(AttackableUnit target)
        {
            if (!Targets.Exists(t => t.Unit == target))
            {
                return false;
            }

            Targets.RemoveAt(Targets.FindIndex(t => t.Unit == target));

            return true;
        }

        /// <summary>
        /// Sets the CastTarget of the given slot to the given unit.
        /// An index outside the bounds of the list will be appended.
        /// </summary>
        /// <param name="target">Unit to input.</param>
        /// <param name="index">Index to set.</param>
        public void SetTarget(AttackableUnit target, int index)
        {
            if (Targets.Count - 1 < index)
            {
                AddTarget(target);
                return;
            }

            Targets[index] = new CastTarget(target, CastTarget.GetHitResult(target, IsAutoAttack, Owner.IsNextAutoCrit, Owner.IsNextAutoMiss, Owner.IsNextAutoDodged));
        }
        public CastInfo Clone()
        {
            var clone = (CastInfo)this.MemberwiseClone();
            if (Targets != null)
            {
                clone.Targets = new List<CastTarget>(Targets);
            }
            return clone;
        }
    }
}