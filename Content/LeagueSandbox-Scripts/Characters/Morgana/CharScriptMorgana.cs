using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptMorgana : ICharScript {
    private ObjAIBase _morgana;
    private Particle _p1, _p2;
    private Vector2 _previousPosition;
    private bool _isIdle = false;
    
    public void OnActivate(ObjAIBase owner, Spell spell) {
        _morgana = owner;
        AddBuff("EmpathizeAura", 25000f, 1, spell, owner, owner, true);
        
        for (short i = 0; i < 4; i++)
        {
            ApiEventManager.OnSpellCast.AddListener(this, _morgana.Spells[i], OnSpellsCast);
        }

        ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnSpellsCast);
    }

    
    public void OnUpdate(float diff)
    {
        if (_morgana.Position != _previousPosition || _morgana.IsAttacking)
        {
            ResetPauseTimer();
            _previousPosition = _morgana.Position;
            return;
        }

        _previousPosition = _morgana.Position;
        if (_isIdle) return;
        ExecutePeriodically(_morgana.CharVars, "idleTick", 5000f, false, 0, () =>
        {
            _p1 = SpellEffectCreate("Morgana_Base_Idle2.troy", _morgana, _morgana, _morgana, lifetime: -1f, flags: FXFlags.SimulateWhileOffScreen);
            _p2 = SpellEffectCreate("Morgana_Base_Idle.troy", _morgana, _morgana, _morgana, lifetime: -1f, flags: FXFlags.SimulateWhileOffScreen);
            _isIdle = true;
        });
    }

    private void OnSpellsCast(Spell spell)
    {
        ResetPauseTimer();
    }

    private void ResetPauseTimer()
    {
        ExecutePeriodicallyReset(_morgana.CharVars, "idleTick");
        if (!_isIdle) return;
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        _isIdle = false;
    }
}