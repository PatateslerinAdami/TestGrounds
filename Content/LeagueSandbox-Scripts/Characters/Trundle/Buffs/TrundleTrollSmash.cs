using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

internal class TrundleTrollSmash : IBuffGameScript {
    private ObjAIBase _trundle;
    private Spell     _spell;
    private Buff      _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _trundle  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _buff  = buff;
        ownerSpell.SetCooldown(0f, true);
        AddParticleTarget(_trundle, _trundle, "Trundle_Q_TrollSmash_buf", _trundle, bone: "head");
        
        // Keep the spell in toggled-on state while this buff is active.
        ownerSpell.SetSpellToggle(true);
        
        // Q empowers the next basic attack spell variant (ExtraSpell1), not the cast spell itself.
        _trundle.SetAutoAttackSpell("TrundleQ", true);

        // Hook damage and hit events to apply bonus damage and splash behavior.
        ApiEventManager.OnHitUnit.AddListener(this, _trundle, OnHit);
        ApiEventManager.OnPreAttack.AddListener(this, _trundle, OnPreAttack);
    }

    public void OnUpdate(float diff) {
        SealSpellSlot(_trundle, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, true);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _trundle.ResetAutoAttackSpell();
        ApiEventManager.RemoveAllListenersForOwner(this);
        ownerSpell.SetCooldown(ownerSpell.CastInfo.Cooldown, false);
        SealSpellSlot(_trundle, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, false);
    }

    private void OnPreAttack(Spell spell) {
    }

    private void OnHit(DamageData data) {
        if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK || !data.IsAutoAttack) return;

        AddParticleTarget(_trundle, _trundle, "Trundle_Q_bite_siliva", _trundle);
        
        var dmg = 20f                              + 20f   * (_spell.CastInfo.SpellLevel - 1);
        data.PostMitigationDamage += dmg;
        var variables = new BuffVariables();
        variables.Set("slowPercent", 0.75f);
        AddBuff("Slow", 0.1f, 1, _spell, data.Target, _trundle, buffVariables: variables);
        AddBuff("TrundleQDebuff", 8f, 1, _spell, data.Target, _trundle);
        AddBuff("TrundleQ", 8f, 1, _spell, _trundle, _trundle);
        _buff.DeactivateBuff();
    }
}
