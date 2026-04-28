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

public class CharScriptTalon : ICharScript {
    private ObjAIBase       _talon;

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _talon = owner;
        ApiEventManager.OnPreDealDamage.AddListener(this, _talon, OnPreDealDamage);
    }

    private void OnPreDealDamage(DamageData damageData) {
        if (!damageData.Target.HasBuffType(BuffType.SLOW)  && !damageData.Target.HasBuffType(BuffType.STUN) &&
            !damageData.Target.HasBuffType(BuffType.SNARE) &&
            !damageData.Target.HasBuffType(BuffType.SUPPRESSION) || damageData.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
        var damageAmp = damageData.PostMitigationDamage;
        damageData.PostMitigationDamage += damageAmp * 0.1f;
    }
}