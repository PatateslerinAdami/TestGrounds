using System;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    // Aatrox Q orchestrator. The actual movement now lives in self-buffs (Riot architecture, replay
    // 663eda09): AatroxQ (ascend) → AatroxQDescent (dive); the enemy airborne is AatroxQKnockup. This
    // script only owns the per-cast state (the clamped end position) + cast cost/particles, and kicks
    // off the ascend buff. CAN_CAST is NOT sealed during Q (Riot keeps it castable).
    public class AatroxQ : ISpellScript
    {
        private ObjAIBase _aatrox;
        private Vector2   _endPos2D;
        private Vector2   _castStartPos2D;
        private const float MaxDashRange = 650f;

        /// <summary>Clamped Q landing position (cast-start + capped direction). Read by the AatroxQ /
        /// AatroxQDescent self-buffs which perform the ascend and dive.</summary>
        public Vector2 EndPos    => _endPos2D;
        public Vector2 CastStart => _castStartPos2D;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _aatrox = owner;
            ApiEventManager.OnUpdateStats.AddListener(this, _aatrox, OnUpdateStats);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var healthCost = _aatrox.Stats.CurrentHealth * 0.1f;
            _aatrox.Stats.CurrentHealth = Math.Max(1, _aatrox.Stats.CurrentHealth - healthCost);
            var buff = _aatrox.GetBuffWithName("AatroxPassive")?.BuffScript as AatroxPassive;
            buff?.AddBlood(healthCost);
            
            _castStartPos2D = owner.Position;
            _endPos2D = end != Vector2.Zero
                ? end
                : start != Vector2.Zero
                    ? start
                    : new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);

            var desiredDirection = _endPos2D - _castStartPos2D;
            var desiredDistance = desiredDirection.Length();
            if (desiredDistance > MaxDashRange)
            {
                _endPos2D = _castStartPos2D + (Vector2.Normalize(desiredDirection) * MaxDashRange);
            }
        }

        public void OnSpellCast(Spell spell)
        {
            PlayAnimation(_aatrox, Vector2.Distance(_aatrox.Position, _endPos2D) <= 250f ? "Spell1_Close" : "Spell1", 1f);
            var allyCircleParticle = _aatrox.SkinID switch {
                1 => "Aatrox_Skin01_Q_Tar_Green",
                2 => "Aatrox_Skin02_Q_Tar_Green",
                _ => "Aatrox_Base_Q_Tar_Green"
            };
            var enemyCircleParticle = _aatrox.SkinID switch {
                1 => "Aatrox_Skin01_Q_Tar_Red",
                2 => "Aatrox_Skin02_Q_Tar_Red",
                _ => "Aatrox_Base_Q_Tar_Red"
            };
            AddParticle(_aatrox, null, allyCircleParticle, _endPos2D, enemyParticle: enemyCircleParticle);
        }

        public void OnSpellPostCast(Spell spell)
        {
            // Kick off the ascend phase; the AatroxQ self-buff drives the leap and hands off to
            // AatroxQDescent for the dive. Script-controlled (Riot wire dur=0): infiniteduration +
            // explicit removal at the phase boundaries — AatroxQ is removed when the descent begins
            // (~0.40s), AatroxQDescent at landing. Replay 663eda09: AatroxQ life ~250-432ms,
            // AatroxQDescent ~250-630ms; both dur=0, never expire by duration.
            AddBuff("AatroxQ", 10f, 1, spell, _aatrox, _aatrox, infiniteduration: true);
        }

        private void OnUpdateStats(AttackableUnit unit, float diff) {
            var cost = _aatrox.Stats.CurrentHealth * 0.1f;
            SetSpellToolTipVar(_aatrox, 2, cost, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        }
    }
}
