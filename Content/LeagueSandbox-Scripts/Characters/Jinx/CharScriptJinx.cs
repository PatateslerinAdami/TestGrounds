using System;
using System.Collections.Concurrent;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptJinx : ICharScript {
    private ObjAIBase       _jinx;
    private Spell _spell;
    

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _jinx = owner;
        _spell = spell;
        ApiEventManager.OnDealDamage.AddListener(this, _jinx, OnDealDamage);
    }
    
    private void OnDealDamage(DamageData data) {
        if (!IsValidTarget(_jinx, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectTurrets)) return;
        AddBuff("JinxPassiveMarker", 4f, 1, _jinx.GetSpell("JinxQ"), data.Target, _jinx);
    }
}