using System;
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

internal class ZedShadowJoke : IBuffGameScript {
    private       ObjAIBase _zed;
    private       Minion    _minion;
    private       float     _timer       = 0f;
    private const float     MaxTime      = 5f;
    private       short     _stepCount   = 0;
    private       short     MaxStepCount = 1;
    private       Random    _random      = new Random();
    private       int       _randomNumber;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _randomNumber = _random.Next(1, 3);
        _zed = unit as ObjAIBase;
        var pos       = GetPointFromUnit(_zed, 25f,  0f);
        var facingPos = GetPointFromUnit(_zed, 100f, 0f);
        _minion = AddMinion(_zed, "ZedShadow", "ZedShadow", pos, _zed.Team, _zed.SkinID, true, false);
        FaceDirection(facingPos, _minion, true);
        PlayAnimation(_minion, _randomNumber == 1 ? "Joke_SH_win" : "Joke_SH_loss", 3f);
    }

    public void OnUpdate(float diff) {
        //Shit fix we need PacketOverrides for Emotions
        _timer += diff;
        if (_timer >= MaxTime && _stepCount < MaxStepCount) {
            PlayAnimation(_zed, _randomNumber == 1  ? "Joke_KG_loss" : "Joke_KG_win", 3f); 
            _stepCount++;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (_minion == null || _minion.IsDead || _minion.IsToRemove()) return;
        AddBuff("ExpirationTimer", 1.5f, 1, ownerSpell, _minion, _minion);
        _minion = null;
    }
}