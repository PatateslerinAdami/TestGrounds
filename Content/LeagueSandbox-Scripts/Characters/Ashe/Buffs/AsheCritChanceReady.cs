using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class AsheCritChanceReady : IBuffGameScript {
    private ObjAIBase _ashe;
    private Buff      _buff;
    private float     _baseCrit;
    private bool      _consumed;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.RENEW_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ashe  = ownerSpell.CastInfo.Owner;
        _buff  = buff;
        _baseCrit = _ashe.Stats.CriticalChance.Total;
        var neededBonus = Math.Max(0f, 1f - _baseCrit);
        StatsModifier.CriticalChance.FlatBonus = neededBonus;
        unit.AddStatModifier(StatsModifier);
        ApiEventManager.OnHitUnit.AddListener(this, _ashe, OnHitUnit);
    }

    private void OnHitUnit(DamageData data) {
        if (_consumed) return;
        if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
        _consumed = true;
        var focus = _ashe?.GetBuffWithName("Focus");
        if (focus != null) {
            var resetStacks = (int) MathF.Round(_baseCrit * 100f);
            resetStacks = Math.Clamp(resetStacks, 0, focus.MaxStacks);
            focus.SetStacks(resetStacks);
        }
        _buff.DeactivateBuff();
    }
    
    

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
