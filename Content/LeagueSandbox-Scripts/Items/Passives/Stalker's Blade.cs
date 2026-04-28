using System.Collections.Generic;
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
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_3706 : IItemScript {
    private ObjAIBase     _owner;
    public  StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        ApiEventManager.OnHitUnit.AddListener(this, owner, OnHit);
        owner.SetSpell("S5_SummonerSmitePlayerGanker", owner.GetSpell("SummonerSmite").CastInfo.SpellSlot, true);
    }
    
    private void OnHit(DamageData data) {
        if (!IsValidTarget(_owner, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral)) return;
        var bufVariables = new BuffVariables();
        bufVariables.Set("damageAmount", 45f);
        bufVariables.Set("healthAmount", 10f);
        bufVariables.Set("manaAmount",   5f);
        AddBuff("ItemMonsterBurn",  2f, 1, _owner.AutoAttackSpell, data.Target, _owner, buffVariables: bufVariables);
        AddBuff("ItemMonsterRegen", 2f, 1, _owner.AutoAttackSpell, _owner,      _owner, buffVariables: bufVariables);
    }

    public void OnDeactivate(ObjAIBase owner) {
        owner.SetSpell("SummonerSmite", owner.GetSpell("S5_SummonerSmitePlayerGanker").CastInfo.SpellSlot, true);
    }
}