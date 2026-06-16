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

// ============================================================================
// R — JayceStanceHtG (Hammer → Cannon) and JayceStanceGtH (Cannon → Hammer)
// ============================================================================

/// <summary>
/// Hammer → Cannon transform (R in melee form).
/// </summary>
public class JayceStanceHtG : ISpellScript
{
    private ObjAIBase _owner;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        _spell = spell;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell)
    {
        AddParticleTarget(_owner, _owner, "Jayce_Base_R_ChangeToCannon.troy", _owner, 1f);
    }
    public void OnSpellPostCast(Spell spell)
    {
        TransformToCannon();
    }

    private void TransformToCannon()
    {
        _owner.ChangeModel("JayceCannon");

        SetSpell(_owner, "JayceShockBlast",      SpellSlotType.SpellSlots, 0, fullReplace: true);
        SetSpell(_owner, "JayceHyperCharge",      SpellSlotType.SpellSlots, 1, fullReplace: true);
        SetSpell(_owner, "JayceAccelerationGate", SpellSlotType.SpellSlots, 2, fullReplace: true);
        SetSpell(_owner, "JayceStanceGtH",        SpellSlotType.SpellSlots, 3, fullReplace: true);

        _owner.SetAutoAttackSpell("JayceRangedAttack",  false);
        _owner.SetAutoAttackSpell("JayceRangedAttack2", false);
    }

    public void OnUpdate(float diff) { }
}

/// <summary>
/// Cannon → Hammer transform (R in ranged form).
/// </summary>
public class JayceStanceGtH : ISpellScript
{
    private ObjAIBase _owner;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        _spell = spell;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell)
    {
        AddParticleTarget(_owner, _owner, "Jayce_Base_R_ChangeToHammer.troy", _owner, 1f);
    }
    public void OnSpellPostCast(Spell spell)
    {
        TransformToHammer();
    }

    private void TransformToHammer()
    {
        _owner.ChangeModel("Jayce");

        SetSpell(_owner, "JayceToTheSkies",      SpellSlotType.SpellSlots, 0, fullReplace: true);
        SetSpell(_owner, "JayceStaticField",      SpellSlotType.SpellSlots, 1, fullReplace: true);
        SetSpell(_owner, "JayceThunderingBlow",   SpellSlotType.SpellSlots, 2, fullReplace: true);
        SetSpell(_owner, "JayceStanceHtG",        SpellSlotType.SpellSlots, 3, fullReplace: true);

        _owner.SetAutoAttackSpell("JayceBasicAttack",  false);
        _owner.SetAutoAttackSpell("JayceBasicAttack2", false);
    }

    public void OnUpdate(float diff) { }
}
