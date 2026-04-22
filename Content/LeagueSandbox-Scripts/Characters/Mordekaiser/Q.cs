using System;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class MordekaiserMaceOfSpades : ISpellScript
{
    private ObjAIBase _mordekaiser;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _mordekaiser = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _mordekaiser, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        _mordekaiser.Stats.CurrentHealth =
            Math.Max(1, owner.Stats.CurrentHealth - (25f + 7 * (_spell.CastInfo.SpellLevel - 1)));

        owner.CancelAutoAttack(true, false);
        AddBuff("MordekaiserMaceOfSpades", 10f, 1, spell, owner, owner);
    }

    private void OnUpdateStats(AttackableUnit target, float diff)
    {
        var ad = _mordekaiser.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        SetSpellToolTipVar(_mordekaiser, 0, ad * 1.65f, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_mordekaiser, 1, ad, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }

    public void OnSpellPostCast(Spell spell)
    {
        spell.SetCooldown(0f, true);
    }
}

public class MordekaiserNukeOfTheBeast : ISpellScript
{
    private ObjAIBase _mordekaiser;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Target
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _mordekaiser = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        var ap = _mordekaiser.Stats.AbilityPower.Total * 0.4f;
        var ad = _mordekaiser.Stats.AttackDamage.FlatBonus;
        var dmg = 80f + 30f * (_mordekaiser.Spells[0].CastInfo.SpellLevel - 1) + ad + ap;
        AddParticleTarget(_mordekaiser, target, "mordakaiser_maceOfSpades_tar2", target);
        target.TakeDamage(_mordekaiser, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
            DamageResultType.RESULT_NORMAL);
    }
}