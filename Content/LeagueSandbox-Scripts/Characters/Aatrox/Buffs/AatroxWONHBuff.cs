using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class AatroxWONHBuff : IBuffGameScript {
    private ObjAIBase _aatrox;
    private Spell     _spell;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COUNTER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks   = 3,
        IsHidden    = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _aatrox = ownerspell.CastInfo.Owner;
        _spell  = ownerspell;
        RemoveBuff(_aatrox, "AatroxWPower");
        ApiEventManager.OnHitUnit.AddListener(this, _aatrox, OnHit);
    }

    private void OnHit(DamageData data) {
        if (!IsValidTarget(_aatrox, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral))return;
        AddBuff("AatroxW", 2500f, 1, _spell, _aatrox, _aatrox, true);
        switch (_aatrox.GetBuffsWithName("AatroxW").Count) {
            case 2:
                AddBuff(_aatrox.HasBuff("AatroxWLife") ? "AatroxWONHLifeBuff" : "AatroxWONHPowerBuff", 25000f, 1,
                        _spell, _aatrox, _aatrox, true);
                break;
            case >= 3:
                var buffs = _aatrox.GetBuffsWithName("AatroxW");
                foreach (var buff in buffs) { RemoveBuff(buff); }

                if (_aatrox.HasBuff("AatroxWLife")) {
                    SpellCast(_aatrox, 2, SpellSlotType.ExtraSlots, false, _aatrox, data.Target.Position);
                }else if (_aatrox.HasBuff("AatroxWPower")) {
                    var adCost = _aatrox.Stats.AttackDamage.FlatBonus * _aatrox.Spells[1].SpellData.Coefficient;
                    var healthCost                  = 15f + 8.75f * (_aatrox.Spells[1].CastInfo.SpellLevel - 1) + adCost;
                    _aatrox.Stats.CurrentHealth = Math.Max(1, _aatrox.Stats.CurrentHealth - healthCost);
                    var buff = _aatrox.GetBuffWithName("AatroxPassive")?.BuffScript as AatroxPassive;
                    buff?.AddBlood(healthCost);

                    var ad  = _aatrox.Stats.AttackDamage.FlatBonus * _aatrox.Spells[1].SpellData.Coefficient2;
                    var dmg = 60f + 35f * (_aatrox.Spells[1].CastInfo.SpellLevel - 1) + ad;
                    data.Target.TakeDamage(_aatrox, dmg, DamageType.DAMAGE_TYPE_PHYSICAL,
                                           DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
                }

                RemoveBuff(_aatrox, _aatrox.HasBuff("AatroxWLife") ? "AatroxWONHLifeBuff" : "AatroxWONHPowerBuff");
                break;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) { }
}