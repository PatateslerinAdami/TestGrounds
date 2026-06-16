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

/// <summary>
/// Nidalee R — Aspect of the Cougar.
/// Toggles between Human and Cougar form.
/// Swaps Q/W/E spells + model + auto-attacks.
/// </summary>
public class AspectOfTheCougar : ISpellScript
{
    private ObjAIBase _owner;
    private bool      _isCougarForm;
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
        _isCougarForm = false;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell)
    {
        if (_isCougarForm)
        {
            SwitchToHuman();
        }
        else
        {
            SwitchToCougar();
        }
    }

    private void SwitchToCougar()
    {
        _owner.ChangeModel("Nidalee_Cougar");

        // Swap human spells → cougar spells
        SetSpell(_owner, "Takedown",  SpellSlotType.SpellSlots, 0, fullReplace: true);
        SetSpell(_owner, "Pounce",    SpellSlotType.SpellSlots, 1, fullReplace: true);
        SetSpell(_owner, "Swipe",     SpellSlotType.SpellSlots, 2, fullReplace: true);
        // Spell 3 (R) stays as AspectOfTheCougar — no need to change

        // Swap auto-attacks to cougar variants
        _owner.SetAutoAttackSpell("Nidalee_CougarBasicAttack",  false);
        _owner.SetAutoAttackSpell("Nidalee_CougarBasicAttack2", false); // ExtraAttack1
        // CritAttack is handled by the engine from character data

        _isCougarForm = true;
        _spell.SetSpellToggle(true);
    }

    private void SwitchToHuman()
    {
        _owner.ChangeModel("Nidalee");

        // Swap cougar spells → human spells
        SetSpell(_owner, "JavelinToss", SpellSlotType.SpellSlots, 0, fullReplace: true);
        SetSpell(_owner, "Bushwhack",   SpellSlotType.SpellSlots, 1, fullReplace: true);
        SetSpell(_owner, "PrimalSurge", SpellSlotType.SpellSlots, 2, fullReplace: true);

        // Swap auto-attacks to human variants
        _owner.SetAutoAttackSpell("NidaleeBasicAttack",  false);
        _owner.SetAutoAttackSpell("NidaleeBasicAttack2", false);

        _isCougarForm = false;
        _spell.SetSpellToggle(false);
    }

    public void OnUpdate(float diff) { }
}
