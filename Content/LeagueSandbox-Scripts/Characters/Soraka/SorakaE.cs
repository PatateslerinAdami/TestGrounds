using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

/// <summary>
/// Soraka E — Equinox (4.17 rework / 4.20).
/// Ground-targeted zone (1.5s). Silences enemies inside on each tick.
/// On expiry: roots + deals final damage to enemies still in zone.
/// Uses SpellSector pattern (matching Katarina W / Rammus Q).
/// </summary>
public class SorakaE : ISpellScript
{
    private const float ZoneDuration = 1.5f;
    private const float ZoneRadius = 250f;

    private ObjAIBase _soraka;
    private Spell _spell;
    private Vector2 _cursorPos;
    private SpellSector _silenceSector;
    private bool _rootPending;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _soraka = owner;
        _spell = spell;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell)
    {
        var owner = spell.CastInfo.Owner;
        _cursorPos = new Vector2(
            spell.CastInfo.TargetPosition.X,
            spell.CastInfo.TargetPosition.Z);

        // Ground VFX
        AddParticle(owner, null, "Soraka_Base_E_tar.troy", _cursorPos, ZoneDuration + 0.5f);
        AddParticle(owner, null, "Soraka_Base_E_rune.troy", _cursorPos, ZoneDuration + 0.5f);

        // Perception bubble so the client can render VFX
        AddPosPerceptionBubble(_cursorPos, ZoneRadius, ZoneDuration + 0.5f, owner.Team);

        // Silence zone — multi-tick, hits every quarter second
        _silenceSector = spell.CreateSpellSector(new SectorParameters
        {
            Width = ZoneRadius * 2,
            Length = ZoneRadius * 2,
            SingleTick = false,
            Lifetime = ZoneDuration,
            Tickrate = 4,
            CanHitSameTarget = true,
            CanHitSameTargetConsecutively = false,
            Type = SectorType.Area,
            OverrideFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes,
        });
        ApiEventManager.OnSpellSectorHit.AddListener(this, _silenceSector, OnSilenceZoneHit, false);

        // Schedule root at expiry — check area directly instead of creating a second sector
        _rootPending = true;
        owner.RegisterTimer(new GameScriptTimer(ZoneDuration, () =>
        {
            if (!_rootPending) return;
            _rootPending = false;

            // Root all enemies still in the zone area
            int rank = _spell.CastInfo.SpellLevel;
            float rootDuration = rank switch
            {
                1 => 1.0f, 2 => 1.25f, 3 => 1.5f, 4 => 1.75f, 5 => 2.0f, _ => 1.0f
            };

            var enemies = GetUnitsInRange(owner, _cursorPos, ZoneRadius, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);

            foreach (var u in enemies)
            {
                if (u.IsDead || u.Team == owner.Team) continue;
                AddBuff("Root", rootDuration, 1, _spell, u, _soraka);
                AddParticleTarget(_soraka, u, "soraka_base_e_snare_tar.troy", u, rootDuration);
            }

            // Explosion VFX at zone center
            AddParticle(owner, null, "Soraka_Base_E_tar.troy", _cursorPos, 0.5f);
        }));
    }

    private void OnSilenceZoneHit(SpellSector sector, AttackableUnit target)
    {
        if (target.IsDead) return;
        if (target.Team == _soraka.Team) return;

        int rank = _spell.CastInfo.SpellLevel;
        float ap = _soraka.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;

        float tickDamage = rank switch
        {
            1 => 70f, 2 => 110f, 3 => 150f, 4 => 190f, 5 => 230f, _ => 70f
        } + ap;

        target.TakeDamage(_soraka, tickDamage, DamageType.DAMAGE_TYPE_MAGICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE, false);

        // Refresh silence while in zone
        if (!target.HasBuff("Silence"))
        {
            AddBuff("Silence", ZoneDuration + 0.5f, 1, _spell, target, _soraka);
        }
    }

    public void OnUpdate(float diff) { }
}
