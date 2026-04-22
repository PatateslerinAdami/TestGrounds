using System;
using System.Linq;
using System.Numerics;
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

internal class MordekaiserMaceOfSpades : IBuffGameScript {
    private ObjAIBase _mordekaiser;
    private Spell     _spell;
    private Buff      _buff;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _mordekaiser                  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _buff = buff;
        
        ApiEventManager.OnHitUnit.AddListener(this, _mordekaiser, OnHit);
    }

    private void OnHit(DamageData data) {
        if (!IsValidTarget(_mordekaiser, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        var targets = GetUnitsInRange(_mordekaiser, data.Target.Position, 600f, true,
                        SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                        SpellDataFlags.AffectNeutral).Where(unit => unit != data.Target).OrderBy(x => Vector2.Distance(data.Target.Position, x.Position)).ToList();
        if (targets.Count != 0) {
            var length = Math.Min(targets.Count, 4);
            for (var i = 0; i < length; i++) {
                SpellCast(_mordekaiser, 2, SpellSlotType.ExtraSlots, true, targets[i], data.Target.Position);
            }
            AddBuff("MordekaiserMaceOfSpadesDmg", 1f, 1, _spell, data.Target, _mordekaiser);
        } else {
            var variables3 = new BuffVariables();
            variables3.Set("isSingleTarget", true);
            AddBuff("MordekaiserMaceOfSpadesDmg", 1f, 1, _spell, data.Target, _mordekaiser, buffVariables: variables3);
        }
        _buff.DeactivateBuff();
    }

    public void OnUpdate(float diff) {
        SealSpellSlot(_mordekaiser, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, true);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        SealSpellSlot(_mordekaiser, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, false);
        _spell.SetCooldown(_spell.CastInfo.Cooldown,  true);
    }
}