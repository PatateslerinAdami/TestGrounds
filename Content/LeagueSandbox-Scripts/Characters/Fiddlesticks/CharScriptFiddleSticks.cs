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
using LeagueSandbox.GameServer.Logging;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptFiddleSticks : ICharScript
{
    private ObjAIBase _fiddlesticks;
    private Vector2 _previousPosition;
    private bool _isPaused = false;

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _fiddlesticks = owner;
        AddBuff("Paranoia", 25000f, 1, spell, owner, owner, true);
        for (short i = 0; i < 4; i++)
        {
            ApiEventManager.OnSpellCast.AddListener(this, _fiddlesticks.Spells[i], OnSpellsCast);
        }

        ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnSpellsCast);
    }

    public void OnUpdate(float diff)
    {
        if (_fiddlesticks.Position != _previousPosition || _fiddlesticks.IsAttacking)
        {
            ResetPauseTimer();
            _previousPosition = _fiddlesticks.Position;
            return;
        }

        _previousPosition = _fiddlesticks.Position;
        if (_isPaused) return;
        ExecutePeriodically(_fiddlesticks.CharVars, "pauseAnimationTick", 5000f, false, 0, () =>
        {
            PauseAnimation(_fiddlesticks, true);
            _isPaused = true;
        });
    }

    private void OnSpellsCast(Spell spell)
    {
        ResetPauseTimer();
    }

    private void ResetPauseTimer()
    {
        ExecutePeriodicallyReset(_fiddlesticks.CharVars, "pauseAnimationTick");
        if (!_isPaused) return;
        PauseAnimation(_fiddlesticks, false);
        _isPaused = false;
    }
}