using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class VayneSilveredBolts : ISpellScript {
    private ObjAIBase _vayne;
    private Spell     _spell;
    private AttackableUnit _lastSilverBoltsTarget;

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _vayne = owner;
        _spell = spell;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        ApiEventManager.OnHitUnit.AddListener(this, _vayne, OnHit);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
    
    private void OnLevelUpSpell(Spell spell) {
        spell.SetSpellToggle(true);
        //AddBuff("VayneSilverBolts", 25000f, 1, _spell, _vayne,  _vayne, true);
        SealSpellSlot(_spell.CastInfo.Owner, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
    }
    
    public void OnUpdate(float diff) {
        SealSpellSlot(_spell.CastInfo.Owner, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
    }
    
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        
    }

    private void OnHit(DamageData data) {
        if (_spell.CastInfo.SpellLevel <= 0) return;
        if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK || !data.IsAutoAttack) return;

        var invalidDamageResult = data.DamageResultType == DamageResultType.RESULT_MISS ||
                                  data.DamageResultType == DamageResultType.RESULT_DODGE ||
                                  data.DamageResultType == DamageResultType.RESULT_INVULNERABLE ||
                                  data.DamageResultType == DamageResultType.RESULT_INVULNERABLENOMESSAGE;
        if (invalidDamageResult) return;
        var targetIsImmune = data.Target is BaseTurret or Inhibitor or Nexus or ObjBuilding;
        if (targetIsImmune) return;

        if (_lastSilverBoltsTarget != null && _lastSilverBoltsTarget != data.Target) {
            RemoveBuff(_lastSilverBoltsTarget, "VayneSilverDebuff");
        }
        _lastSilverBoltsTarget = data.Target;

        var currentStacks = data.Target.GetBuffWithName("VayneSilverDebuff")?.StackCount ?? 0;
        if (currentStacks >= 2) {
            AddBuff("VayneSilveredDebuff", 0.25f, 1, _spell, data.Target, _vayne);
            // STACKS_AND_RENEWS so we have to clear all stacks after the proc.
            RemoveBuff(data.Target, "VayneSilverDebuff");
            AddParticleTarget(_vayne, data.Target, "vayne_W_tar", data.Target);
        } else { 
            AddBuff("VayneSilverDebuff", 3.5f, 1, _spell, data.Target, _vayne);
        }
    }
}
