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

internal class BlackShield : IBuffGameScript {
    private ObjAIBase _morgana;
    private Shield    _blackShield;
    private Particle  _blackShieldParticle;
    private Buff      _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SPELL_IMMUNITY,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _morgana             = ownerSpell.CastInfo.Owner;
        _buff                = buff;
        _blackShieldParticle = AddParticleTarget(_morgana, unit, "Morgana_Base_E_Tar", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CENTER_LOC");
        ApiEventManager.OnAllowAddBuff.AddListener(this, unit, OnAllowAddBuff);
        var shieldHealth = 70f + 70f * (ownerSpell.CastInfo.SpellLevel - 1) + _morgana.Stats.AbilityPower.Total * 0.7f;
        _blackShield = new Shield(_morgana, _morgana, false, true, shieldHealth);
        _morgana.AddShield(_blackShield);
        ApiEventManager.OnShieldBreak.AddListener(this, _blackShield, OnShieldBreak);
    }

    private bool OnAllowAddBuff(AttackableUnit target, AttackableUnit unit, Buff buff) { return buff.BuffType is not (BuffType.BLIND or BuffType.CHARM or BuffType.DISARM or BuffType.FEAR or BuffType.FLEE or BuffType.FRENZY or BuffType.KNOCKBACK or BuffType.KNOCKUP or BuffType.NEAR_SIGHT or BuffType.POLYMORPH or BuffType.SLEEP or BuffType.SILENCE or BuffType.SLOW or BuffType.SNARE or BuffType.STUN or BuffType.TAUNT or BuffType.SUPPRESSION); }

    private void OnShieldBreak(Shield shield) {
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        _morgana.RemoveShield(_blackShield);
        RemoveParticle(_blackShieldParticle);
    }
}