using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class PantheonRJump : ISpellScript
{
    private ObjAIBase _pantheon;
    private Vector2 _targetPos;
    private Particle _p, _p2, _p3;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        ChannelDuration = 2f,
        IsDamagingSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _pantheon = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _targetPos = new Vector2(end.X, end.Y);
        switch (_pantheon.Team)
        {
            case TeamId.TEAM_BLUE:
                _p = AddParticle(_pantheon, null, "Pantheon_Base_R_indicator_green", _targetPos,
                    teamOnly: TeamId.TEAM_BLUE, lifetime: 4.5f);
                break;
            case TeamId.TEAM_PURPLE:
                _p = AddParticle(_pantheon, null, "Pantheon_Base_R_indicator_green", _targetPos,
                    teamOnly: TeamId.TEAM_PURPLE, lifetime: 4.5f);
                break;
        }

        SealSpellSlot(_pantheon, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, true);
        SealSpellSlot(_pantheon, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
        SealSpellSlot(_pantheon, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, true);
        FaceDirection(_targetPos, _pantheon);
    }

    public void OnSpellChannel(Spell spell)
    {
        _p3 = AddParticleTarget(_pantheon, _pantheon, "Pantheon_Base_R_cas", _pantheon);
        AddBuff("Pantheon_AegisShield", 15000f, 1, spell, _pantheon, _pantheon, true);
    }

    public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
    {
        RemoveParticle(_p);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
        SealSpellSlot(_pantheon, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, false);
        SealSpellSlot(_pantheon, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
        SealSpellSlot(_pantheon, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, false);
        spell.SetCooldown(30f, true);
        _pantheon.Stats.CurrentMana += 50f;
    }


    public void OnSpellPostChannel(Spell spell)
    {
        AddPosPerceptionBubble(_targetPos, 700f, 6, _pantheon.Team, false);
        SpellCast(_pantheon, 1, SpellSlotType.ExtraSlots, _targetPos, _targetPos, false, Vector2.Zero);
        switch (_pantheon.Team)
        {
            case TeamId.TEAM_BLUE:
                _p2 = AddParticle(_pantheon, null, "Pantheon_Base_R_indicator_red", _targetPos,
                    teamOnly: TeamId.TEAM_PURPLE, lifetime: 3f);
                break;
            case TeamId.TEAM_PURPLE:
                _p2 = AddParticle(_pantheon, null, "Pantheon_Base_R_indicator_red", _targetPos,
                    teamOnly: TeamId.TEAM_BLUE, lifetime: 3f);
                break;
        }
    }
}

public class PantheonRFall : ISpellScript
{
    private ObjAIBase _pantheon;
    private Vector2 _targetPos;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        IsDamagingSpell = true,
        ChannelDuration = 1.5f,
        NotSingleTargetSpell = true,
        DoesntBreakShields = false,
    };

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _pantheon = owner;
        _targetPos = new Vector2(end.X, end.Y);
        AddBuff("Pantheon_GrandSkyfall", 2f, 1, spell, _pantheon, _pantheon);
        MoveCamera(owner, 0.5f, spell.CastInfo.TargetPosition);
        //Todo: remove projectiles flying towards him while he jumps
    }

    public void OnSpellChannel(Spell spell)
    {
    }

    public void OnSpellPostChannel(Spell spell)
    {
        _pantheon.SetStatus(StatusFlags.CanAttack, true);
        _pantheon.SetStatus(StatusFlags.Targetable, true);
        _pantheon.SetStatus(StatusFlags.NoRender, false);
        _pantheon.SetStatus(StatusFlags.Invulnerable, false);
        SealSpellSlot(_pantheon, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, false);
        SealSpellSlot(_pantheon, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
        SealSpellSlot(_pantheon, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, false);

        _pantheon.TeleportTo(_targetPos);
        AddParticle(_pantheon, null, "Pantheon_Base_R_aoe_explosion", _pantheon.Position);

        var units = GetUnitsInRange(_pantheon, _pantheon.Position, 700, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions);
        foreach (var unit in units)
        {
            var apMin = _pantheon.Stats.AbilityPower.Total * 0.5f;
            var apMax = _pantheon.Stats.AbilityPower.Total;
            var minDamage = 200f + 150f * (_pantheon.GetSpell("PantheonRJump").CastInfo.SpellLevel - 1) + apMin;
            var maxDamage = 400f + 300f * (_pantheon.GetSpell("PantheonRJump").CastInfo.SpellLevel - 1) + apMax;
            Vector2 toEnemy = unit.Position - _pantheon.Position;
            float distance = toEnemy.Length();
            float damage = 200f;
            if (distance <= 250f)
            {
                damage = maxDamage;
            }
            else
            {
                float factor = 1 - ((distance - 250f) / (700 - 250f));
                damage = minDamage + factor * (maxDamage - minDamage);
            }

            unit.TakeDamage(_pantheon, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                false);

            var variables = new BuffVariables();
            variables.Set("slowAmount", 0.3f);
            AddBuff("Slow", 1f, 1, spell, unit, _pantheon, buffVariables: variables);
        }
    }
}