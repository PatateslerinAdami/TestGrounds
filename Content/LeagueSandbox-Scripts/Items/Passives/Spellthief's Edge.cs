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

public class ItemID_3303 : IItemScript {
    private static long          _timer15, _timer30;
    private        int           _count;
    private        bool          _minionKilled;
    private        ObjAIBase     _owner;
    private        Spell         _spell;
    public         StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;

        for (short i = 0; i < 4; i++) {
            _spell = owner.Spells[i];
            if (_spell is null) break;
            LoggerProvider.GetLogger().Info(_spell.SpellName);
            ApiEventManager.OnDealDamage.AddListener(this, owner, TargetExecute);
            ApiEventManager.OnSpellHit.AddListener(this, _spell, TargetExecute);
        }

        _timer15 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        _timer30 = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        ApiEventManager.OnHitUnit.AddListener(this, owner, TargetExecute);
        //On Deal dmg doesn't work
        ApiEventManager.OnDealDamage.AddListener(this, owner, TargetExecute);
        // ApiEventManager.OnHitUnit.AddListener(this, owner, TargetExecute);
        ApiEventManager.OnKillUnit.AddListener(this, owner, TargetExecute);
        // foreach (KeyValuePair<short, Spell> pair in owner.Spells)
        // ApiEventManager.OnSpellHit.AddListener(this, pair.Value, TargetExecute);
    }

    public void OnDeactivate(ObjAIBase owner) {
        ApiEventManager.OnDealDamage.RemoveListener(this);
        ApiEventManager.OnHitUnit.RemoveListener(this);
        ApiEventManager.OnKillUnit.RemoveListener(this);
    }

    public void OnUpdate(float diff) {
        if (!_minionKilled) {
            //Timer for 30 seconds
            var currentTime30 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (currentTime30 - _timer30 < 30000) return;
            LoggerProvider.GetLogger().Info("30 finished");
            _timer30 = currentTime30;
            _count   = 0;
        } else {
            //Timer for 15 seconds
            var currentTime15 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (currentTime15 - _timer15 < 15000) return;
            LoggerProvider.GetLogger().Info("15 finished");
            _timer15      = currentTime15;
            _timer30      = currentTime15;
            _count        = 0;
            _minionKilled = false;
        }
    }

    public void TargetExecute(DamageData data) {
        if (!data.IsAutoAttack || data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
        var ch = data.Attacker as Champion;
        if ((_count < 3 && data.Target is Champion) || _count < 3 || data.Target is BaseTurret) {
            ch?.AddGold(ch, 5.0f);
            data.Target.TakeDamage(data.Attacker, 10f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
                                   false);
            _count++;
        }
    }

    public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ch = _owner as Champion;
        if ((_count < 3 && target is Champion) || (_count < 3 && target is BaseTurret)) {
            ch.AddGold(ch, 5f);
            target.TakeDamage(spell.CastInfo.Owner, 10, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
                              false);
            _count++;
        }
    }

    public void TargetExecute(DeathData data) {
        if (data.Unit is not Minion) return;
        _minionKilled = true;
        _count        = 3;
    }
}
