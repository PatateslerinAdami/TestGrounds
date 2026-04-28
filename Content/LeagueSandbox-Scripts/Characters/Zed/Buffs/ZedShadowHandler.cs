using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class ZedShadowHandler : IBuffGameScript {
    private const float SwapRange              = 1100.0f;
    private const float WShadowLifetimeMs      = 4000f;
    private const float RShadowLifetimeMs      = 6500f;
    private const float ExpiringLeadTimeMs     = 1000f;
    private const int   IndicatorStateFar      = 0;
    private const int   IndicatorStateExpiring = 1;
    private const int   IndicatorStateNear     = 2;

    private Particle  _currentWIndicator, _currentRIndicator;
    private int       _previousWIndicatorState, _previousRIndicatorState;
    private Minion    _wShadow;
    private Minion    _rShadow;
    private ObjAIBase _zed;
    private Spell     _spell;
    private float     _wTimer = 0f;
    private float     _rTimer = 0f;

    private Buff _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.RENEW_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff  = buff;
        _zed   = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
    }

    public void AddWShadow(Minion wShadow) {
        if (_wShadow != null) {
            KillShadow(_wShadow); 
            _wShadow = null;
        }

        AddParticleTarget(wShadow.Owner, wShadow, "zed_base_w_tar", wShadow);

        _currentWIndicator = AddParticleTarget(wShadow.Owner, wShadow.Owner, "zed_shadowindicatorfar", wShadow,
                                              -1,             flags: FXFlags.TargetDirection, unitOnly: _zed);
        _wShadow = wShadow;
        _wTimer  = 0f;
        _previousWIndicatorState = -1;
    }

    public void AddRShadow(Minion rShadow) {
        if (_rShadow != null) {
            KillShadow(_rShadow);
            _rShadow = null;
        }

        _currentRIndicator = AddParticleTarget(rShadow.Owner, rShadow.Owner, "zed_shadowindicatorfar", rShadow,
                                              -1,             flags: FXFlags.TargetDirection, unitOnly: _zed);
        _rShadow = rShadow;
        _rTimer  = 0f;
        _previousRIndicatorState = -1;
    }

    public Minion GetWShadow() {
        return _wShadow;
    }

    public void RemoveWShadow() {
        if (_wShadow == null) return;
        KillShadow(_wShadow);
        _wShadow = null;
    }

    
    public Minion GetRShadow() {
        return _rShadow;
    }

    public void RemoveRShadow() {
        if (_rShadow == null) return;
        KillShadow(_rShadow);
        _rShadow = null;
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }

    public void OnUpdate(float diff) {
        if (_wShadow != null) {
            if (_wShadow.IsDead || _wShadow.IsToRemove()) {
                _currentWIndicator?.SetToRemove();
                _currentWIndicator = null;
                _wShadow           = null;
                _wTimer            = 0f;
            }
        }

        if (_rShadow != null) {
            if (_rShadow.IsDead || _rShadow.IsToRemove()) {
                _currentRIndicator?.SetToRemove();
                _currentRIndicator = null;
                _rShadow           = null;
                _rTimer            = 0f;
            }
        }

        if (_wShadow != null) {
            _wTimer += diff;
            if (_wTimer >= WShadowLifetimeMs) {
                KillShadow(_wShadow);
                _wShadow = null;
                return;
            }
            CheckState(_wShadow);
        }

        if (_rShadow != null) {
            _rTimer += diff;
            if (_rTimer >= RShadowLifetimeMs) {
                KillShadow(_rShadow);
                _rShadow = null;
                return;
            }
            CheckState(_rShadow);
        }
    }

    private void CheckState(Minion shadow) {
        if (shadow == _wShadow) {
            var state = GetIndicatorState(shadow);
            if (state == _previousWIndicatorState) return;
            _previousWIndicatorState = state;
            _currentWIndicator?.SetToRemove();

            _currentWIndicator = AddParticleTarget(shadow.Owner, shadow.Owner, GetIndicatorName(state), shadow,
                                                  _buff.Duration - _buff.TimeElapsed,
                                                  flags: FXFlags.TargetDirection, unitOnly: _zed);
        }else if (shadow == _rShadow) {
            var state = GetIndicatorState(shadow);
            if (state == _previousRIndicatorState) return;
            _previousRIndicatorState = state;
            _currentRIndicator?.SetToRemove();

            _currentRIndicator = AddParticleTarget(shadow.Owner, shadow.Owner, GetIndicatorName(state), shadow,
                                                   _buff.Duration - _buff.TimeElapsed,
                                                   flags: FXFlags.TargetDirection, unitOnly: _zed);
        }
        
    }

    public void KillShadow(Minion shadow) {
        if (shadow == null) return;

        if (shadow == _wShadow) {
            _currentWIndicator?.SetToRemove();
            _currentWIndicator = null;
            _wTimer = 0f;
        } else {
            _currentRIndicator?.SetToRemove();
            _currentRIndicator = null;
            _rTimer = 0f;
        }
        AddParticle(_zed, null, "Zed_CloneDeath", shadow.Position, bone: "head");
        AddBuff("ExpirationTimer", 1f, 1, _spell, shadow, shadow);
    }

    private int GetIndicatorState(Minion shadow) {
        var remainingLifetime = GetRemainingLifetimeMs(shadow);
        if (remainingLifetime <= ExpiringLeadTimeMs) return IndicatorStateExpiring;

        var dist = Vector2.Distance(shadow.Owner.Position, shadow.Position);
        return dist >= SwapRange ? IndicatorStateFar : IndicatorStateNear;
    }

    private float GetRemainingLifetimeMs(Minion shadow) {
        if (shadow == _wShadow) return WShadowLifetimeMs - _wTimer;
        if (shadow == _rShadow) return RShadowLifetimeMs - _rTimer;
        return float.MaxValue;
    }

    private string GetIndicatorName(int state) {
        return state switch {
            IndicatorStateFar      => "zed_shadowindicatorfar",
            IndicatorStateExpiring => "zed_shadowindicatormed",
            IndicatorStateNear     => "zed_shadowindicatornearbloop",
            _ => "zed_shadowindicatorfar"
        };
    }
}
