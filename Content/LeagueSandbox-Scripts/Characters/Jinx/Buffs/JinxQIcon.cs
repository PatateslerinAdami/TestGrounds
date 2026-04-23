using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// Temporary lockout/transition buff after Fishbones is forced off.
internal class JinxQIcon : IBuffGameScript {
    private ObjAIBase _jinx;
    private Spell     _spell;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        ownerSpell.SetSpellToggle(false);
        
        // Return to Pow-Pow autos and base animation state set.
        _jinx.ResetAutoAttackSpell();
        _jinx.SetAnimStates(new Dictionary<string, string> {
            { "idle1_base", "" },
            { "idle2_base", "" },
            { "idle3_base", "" },
            { "idle1", "" },
            { "run", "" },
            { "run_base", "" },
            { "attack1", "" },
            { "attack2", "" }
        });
        
        // Listen for valid basic attacks so we can move into ramp state.
        ApiEventManager.OnHitUnit.AddListener(this, _jinx, OnHit);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }

    private void OnHit(DamageData data) {
        // Only react to Jinx's own basic attack hits while this buff is still present.
        if (data.Attacker != _jinx
            || !data.IsAutoAttack
            || data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK
            || !_jinx.HasBuff("JinxQIcon")) { return; }

        // Transition into the short Q ramp window after a valid hit.
        AddBuff("JinxQRamp", 2.5f, 1, _spell, _jinx, _jinx);
    }
}
