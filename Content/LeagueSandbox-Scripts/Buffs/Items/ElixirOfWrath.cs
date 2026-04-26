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
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class ElixirOfWrath : IBuffGameScript {
    private Particle  _potion;
    private ObjAIBase _owner;
    private Buff      _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _owner = ownerSpell.CastInfo.Owner;
        _buff  = buff;
        ApiEventManager.OnDealDamage.AddListener(_owner, unit, TargetExecute);
        StatsModifier.AttackDamage.FlatBonus = 25f;
        unit.AddStatModifier(StatsModifier);
        _potion = AddParticleTarget(_owner, unit, "Global_Item_ElixirOfWrath_Buf", unit, buff.Duration,
                                    bone: "C_BUFFBONE_GLB_CENTER_LOC");
        ApiEventManager.OnDealDamage.AddListener(this, _owner, TargetExecute);
        ApiEventManager.OnKill.AddListener(this, unit, OnKill);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _potion.SetToRemove(); 
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    private void TargetExecute(DamageData data) {
        if (data.DamageType != DamageType.DAMAGE_TYPE_PHYSICAL) return;
        data.Attacker.TakeHeal(_owner, data.PostMitigationDamage * 0.10f, HealType.PhysicalVamp); ;
    }

    private void OnKill(DeathData data) {
        //TODO: _buff.ExtendDuration(30f);
    }
}