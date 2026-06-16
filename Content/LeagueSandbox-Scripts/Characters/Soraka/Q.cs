using System;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SorakaQ : ISpellScript
{
    private const float Radius = 265f;
    private const float InnerR = 110f;
    private const float MaxRange = 950f;

    private ObjAIBase _owner;
    private Vector2 _targetPos;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _targetPos = new Vector2(end.X, end.Y);
    }

    public void OnSpellCast(Spell spell)
    {
        // Cast VFX — Soraka raises her hand
        AddParticleTarget(_owner, _owner, "Soraka_Base_Q_cas.troy", _owner, lifetime: 0.8f);

        // Beam/arc from Soraka to target
        AddParticlePos(_owner, "Soraka_Base_Q_mis_accelerate.troy",
            _owner.Position, _targetPos,
            lifetime: 0.6f, direction: _owner.Direction);
    }

    public void OnSpellPostCast(Spell spell)
    {
        var pos = _targetPos;

        // Travel time scales with distance: 0.25s (close) → 1s (max range 950)
        float dist = Vector2.Distance(_owner.Position, pos);
        float travelTime = 0.25f + (Math.Min(dist, MaxRange) / MaxRange) * 0.75f;

        // Grant sight of the target area during the drop
        AddPosPerceptionBubble(pos, 300f, travelTime, _owner.Team);

        // Star visible at target during fall
        AddParticle(_owner, null, "Soraka_Base_Q_Mis.troy", pos, travelTime);

        // Defer impact to after travel time
        _owner.RegisterTimer(new GameScriptTimer(travelTime, () =>
        {
            Impact(spell, pos);
        }));
    }

    private void Impact(Spell spell, Vector2 pos)
    {
        int rank = spell.CastInfo.SpellLevel;
        float ap = _owner.Stats.AbilityPower.Total * 0.35f;
        float dmg = (rank switch { 1 => 70, 2 => 110, 3 => 150, 4 => 190, 5 => 230, _ => 70 }) + ap;

        bool hasInnerHit = false;
        int champHits = 0;

        foreach (var u in GetUnitsInRange(_owner, pos, Radius, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral))
        {
            if (u.Team == _owner.Team) continue;

            float dist = Vector2.Distance(pos, u.Position);
            bool isInner = dist <= InnerR;
            u.TakeDamage(_owner, isInner ? dmg * 1.5f : dmg,
                DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, isInner);

            // Slow: 30/35/40/45/50% for 2s
            var slowVars = new BuffVariables();
            slowVars.Set("slowPercent", 0.25f + 0.05f * rank);
            AddBuff("Slow", 2f, 1, spell, u, _owner, buffVariables: slowVars);

            if (isInner && u is Champion)
                hasInnerHit = true;
            if (u is Champion)
                champHits++;
        }

        // Impact VFX — crit if inner ring hit
        AddParticle(_owner, null,
            hasInnerHit ? "Soraka_Base_Q_Tar_crit.troy" : "Soraka_Base_Q_Tar.troy",
            pos, 1.5f);

        // Heal return per champion hit
        float missingHpPct = 1f - _owner.Stats.CurrentHealth / _owner.Stats.HealthPoints.Total;
        float baseH = rank switch { 1 => 25, 2 => 35, 3 => 45, 4 => 55, 5 => 65, _ => 25 };
        float maxH = rank switch { 1 => 50, 2 => 70, 3 => 90, 4 => 110, 5 => 130, _ => 50 };

        for (int i = 0; i < champHits; i++)
        {
            float heal = Math.Min(baseH * (1f + missingHpPct), maxH);
            _owner.TakeHeal(_owner, heal, HealType.SelfHeal);
        }

        if (champHits > 0)
        {
            AddParticleTarget(_owner, _owner, "global_ss_heal_02.troy", _owner, lifetime: 0.5f);
        }
    }

    public void OnUpdate(float diff) { }
}
