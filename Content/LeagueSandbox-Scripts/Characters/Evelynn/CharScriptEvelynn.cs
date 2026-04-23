using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptEvelynn : ICharScript {
    private ObjAIBase _evelynn;
    private Spell     _spell;

    public void OnPostActivate(ObjAIBase owner, Spell spell) {
        _evelynn = owner;
        _spell   = spell;
        ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage);
        ApiEventManager.OnDealDamage.AddListener(this, _evelynn, OnDealDamage);
        ApiEventManager.OnLaunchAttack.AddListener(this, _evelynn, OnLaunchAttack);
        RegisterStealthBreakingSpellListeners();

        AddBuff("EvelynnStealthMarker", 25000f, 1, _spell, _evelynn, _evelynn, true);
    }

    private void RegisterStealthBreakingSpellListeners() {
        short[] watchedSlots = [0, 1, 2, 3, 4, 5, 13];
        foreach (var slot in watchedSlots) {
            if (!_evelynn.Spells.TryGetValue(slot, out var spell) || spell == null) {
                continue;
            }

            ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellCast);
            ApiEventManager.OnSpellChannel.AddListener(this, spell, OnSpellChannel);
        }
    }

    private void OnTakeDamage(DamageData damageData) {
        StartStealthCooldown();
    }

    private void OnDealDamage(DamageData damageData) {
        StartStealthCooldown();
    }

    private void OnLaunchAttack(Spell spell) {
        StartStealthCooldown();
    }

    private void OnSpellCast(Spell spell) {
        StartStealthCooldown();
    }

    private void OnSpellChannel(Spell spell) {
        StartStealthCooldown();
    }

    private void StartStealthCooldown() {
        if (_evelynn == null || _spell == null) {
            return;
        }

        var cd = _evelynn.Stats.Level switch {
            < 6  => 6f,
            < 11 => 5f,
            < 16 => 4f,
            _    => 3f
        };

        _spell.SetCooldown(cd, true);
        AddBuff("EvelynnPassive", cd, 1, _spell, _evelynn, _evelynn);
    }
}
