using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class VayneTumble : IBuffGameScript {
    private ObjAIBase              _vayne;
    private Spell          _spell;
    private Buff           _buff;

    private static float GetTumbleAdRatio(int spellLevel) {
        return spellLevel switch {
            5 => 0.50f,
            4 => 0.45f,
            3 => 0.40f,
            2 => 0.35f,
            _ => 0.30f
        };
    }

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _vayne = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _buff  = buff;
        
        if (_vayne.HasBuff("VayneInquisition")) {
            _vayne.SetAnimStates(new Dictionary<string, string> {
                { "Idle1", "Idle_TumbleUlt" },
                { "Idle2", "Idle_TumbleUlt" },
                { "Idle3", "Idle_TumbleUlt" },
                { "Idle4", "Idle_TumbleUlt" },
                { "Run", "Run_TumbleUlt" },
                { "Attack1", "Attack_TumbleUlt" },
                { "Attack2", "Attack_TumbleUlt" }
            });
            _vayne.SetAutoAttackSpell("VayneTumbleUltAttack", true);
        } else {
            _vayne.SetAnimStates(new Dictionary<string, string> {
                { "Idle1", "Idle_Tumble" },
                { "Idle2", "Idle_Tumble" },
                { "Idle3", "Idle_Tumble" },
                { "Idle4", "Idle_Tumble" },
                { "Run", "Run_Tumble" },
                { "Attack1", "Attack_Tumble" },
                { "Attack2", "Attack_Tumble" }
            });
            _vayne.SetAutoAttackSpell("VayneTumbleAttack", true);
        }
        ApiEventManager.OnHitUnit.AddListener(this, _vayne, OnHit);
    }

    private void OnHit(DamageData data) {
        if (data == null || data.Attacker != _vayne) return;
        if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK || !data.IsAutoAttack) return;
        
        var invalidDamageResult = data.DamageResultType == DamageResultType.RESULT_MISS ||
                                  data.DamageResultType == DamageResultType.RESULT_DODGE ||
                                  data.DamageResultType == DamageResultType.RESULT_INVULNERABLE ||
                                  data.DamageResultType == DamageResultType.RESULT_INVULNERABLENOMESSAGE;
        
        var targetIsStructure = data.Target is BaseTurret or Inhibitor or Nexus;

        if (!invalidDamageResult && !targetIsStructure && data.Target != null) {
            var level      = _spell?.CastInfo.SpellLevel ?? 1;
            var bonusRatio = GetTumbleAdRatio(level);
            var bonusRaw   = _vayne.Stats.AttackDamage.Total * bonusRatio;
            var bonusPostMitigation = data.Target.Stats.GetPostMitigationDamage(
                bonusRaw,
                DamageType.DAMAGE_TYPE_PHYSICAL,
                _vayne
            );
            data.PostMitigationDamage += bonusPostMitigation;
        }

        if (_vayne.HasBuff("VayneInquisitionStealth")) {
            RemoveBuff(_vayne,"VayneInquisitionStealth");
        }
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (_vayne.HasBuff("VayneInquisition")) {
            _vayne.SetAnimStates(new Dictionary<string, string> {
                { "Idle1", "Idle_Ult" },
                { "Idle2", "Idle_Ult" },
                { "Idle3", "Idle_Ult" },
                { "Idle4", "Idle_Ult" },
                { "Run", "Run_Ult" },
                { "Attack1", "Attack_Ult" },
                { "Attack2", "Attack_Ult" }
            });
            _vayne.SetAutoAttackSpell("VayneUltAttack", false);
        } else {
            _vayne.SetAnimStates(new Dictionary<string, string> {
                { "Idle1", "" },
                { "Idle2", "" },
                { "Idle3", "" },
                { "Idle4", "" },
                { "Run", "" },
                { "Attack1", "" },
                { "Attack2", "" }
            });
            _vayne.ResetAutoAttackSpell();
        }
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
