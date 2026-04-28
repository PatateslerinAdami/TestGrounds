using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;

namespace ItemPassives;

public class ItemID_3092 : IItemScript {
    private static long          _timer30;
    private static int           _count;
    private        ObjAIBase     _owner;
    private        Spell         _spell;
    public         StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;

        for (short i = 0; i < 4; i++) {
            _spell = owner.Spells[i];
            if (_spell is null) break;
            ApiEventManager.OnSpellHit.AddListener(this, _spell, TargetExecute);
        }

        _timer30 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        ApiEventManager.OnHitUnit.AddListener(this, owner, TargetExecute);
    }

    public void OnDeactivate(ObjAIBase owner) {
        for (short i = 0; i < 4; i++) {
            _spell = owner.Spells[i];
            if (_spell is null) break;
            ApiEventManager.OnSpellHit.RemoveListener(this, _spell, TargetExecute);
        }

        ApiEventManager.OnHitUnit.RemoveListener(this);
        ApiEventManager.OnKillUnit.RemoveListener(this);
    }

    public void OnUpdate(float diff) {
        //Timer for 30 seconds
        var currentTime30 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (currentTime30 - _timer30 < 30000) return;
        LoggerProvider.GetLogger().Info("30 finished");
        _timer30 = currentTime30;
        _count   = 0;
    }

    public void TargetExecute(DamageData data) {
        if (!data.IsAutoAttack || data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
        var ch = data.Attacker as Champion;
        if ((_count < 3 && data.Target is Champion) || (_count < 3 && data.Target is BaseTurret)) {
            ch.AddGold(ch, 10f);
            data.Target.TakeDamage(data.Attacker, 15, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
                                   false);
            _count++;
        }
    }

    public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ch = _owner as Champion;
        if ((_count < 3 && target is Champion) || (_count < 3 && target is BaseTurret)) {
            ch.AddGold(ch, 10f);
            target.TakeDamage(spell.CastInfo.Owner, 15, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
                              false);
            _count++;
        }
    }
}
