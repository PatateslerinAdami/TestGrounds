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

// Spell Shield — blocks the next enemy ability. Riot mechanism (project_spell_shield_system memory):
// the shield buff hooks buff-adds on its carrier and consumes incoming break-attempt buffs
// ("SpellShieldMarker" from BBBreakSpellShields, or per-spell "*SpellShieldCheck" window buffs),
// removing both the break buff and itself. The blocked spell's script then sees its check buff
// gone and skips the real effect.
internal class SivirE : IBuffGameScript {
    private ObjAIBase _sivir;
    private Spell _ownerSpell;
    private Buff _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SPELL_SHIELD,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _sivir = buff.SourceUnit;
        _ownerSpell = ownerSpell;
        _buff = buff;
        // Shield FX (Sivir_Base_E_shield.troy) is client-automatic: SivirE.luaobj sets it as
        // AutoBuffActivateEffect, so the client plays it on BuffAdd2 without a server particle.
        // Primary consume path: the engine gate in Spell.ApplyEffects notifies us here.
        ApiEventManager.OnSpellShieldBroken.AddListener(this, buff, OnShieldBroken, false);
        // BB-parity path: consume script-applied break buffs (SpellShieldMarker / *SpellShieldCheck)
        // landing on the carrier. Dormant with the engine gate active (blocked executions never
        // reach the script that would add a check buff), kept for BBBreakSpellShields fidelity.
        ApiEventManager.OnUnitBuffActivated.AddListener(this, unit, OnCarrierBuffActivated, false);
    }

    private void OnShieldBroken(Buff shieldBuff, Spell blockedSpell) {
        Consume();
    }

    // No source/team filter: Riot's hook matches purely by buff name (BBBreakSpellShields adds the
    // marker with target as both source and target, so a team check would never match the attacker).
    private void OnCarrierBuffActivated(AttackableUnit unit, Buff incoming) {
        if (!IsSpellShieldBreaker(incoming)) {
            return;
        }

        // Block the hit: consume the break-attempt buff and the shield itself (single-use).
        RemoveBuff(incoming);
        Consume();
    }

    private void Consume() {
        // Both on-block FX are replay-verified server-sent (FX_Create_Group at the consume instant).
        AddParticleTarget(_sivir, _sivir, "Sivir_base_E_proc.troy", _sivir, bone: "Buffbone_Glb_Ground_Loc", flags: FXFlags.SimulateWhileOffScreen);
        RemoveBuff(_buff);

        // 4.20 SivirE.json Effect2 (80/95/110/125/140): mana restored when a spell is blocked.
        AddParticleTarget(_sivir, _sivir, "Sivir_base_E_manaback.troy", _sivir, flags: FXFlags.SimulateWhileOffScreen);
        _sivir.Stats.CurrentMana += _ownerSpell.SpellData.EffectLevelAmount[2][_ownerSpell.CastInfo.SpellLevel];
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        // Listener cleanup is engine-side: DeactivateBuff calls RemoveAllListenersForOwner(BuffScript).
    }
}
