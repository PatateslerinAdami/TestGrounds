using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptTeemo : ICharScript {
    private ObjAIBase         _teemo;
    private Spell             _spell;
    private Vector2   _previousPosition;
    private PeriodicTicker _periodicTicker;
    private float     _stealthTimer;
    private bool      _stealthTimerEnable;

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _teemo = owner;
        _spell = spell;
        ApiEventManager.OnSpellCast.AddListener(this, owner.GetSpell("BlindingDart"), OnSpellsCast);
        ApiEventManager.OnSpellCast.AddListener(this, owner.GetSpell("BantamTrap"),   OnSpellsCast);
        ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnSpellsCast);
        ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage);
    }

    public void OnUpdate(float diff) {
        if (_teemo.Position != _previousPosition || _teemo.IsAttacking) {
            BreakStealth();
            _previousPosition = _teemo.Position;
            return;
        }

        _stealthTimerEnable = true;
        _previousPosition = _teemo.Position;
        if (!_stealthTimerEnable || _teemo.HasBuff("CamouflageStealth")) return;
        var ticks = _periodicTicker.ConsumeTicks(diff, 2000f, false);
        if (ticks != 1) return;
        AddBuff("CamouflageStealth", 100000f, 1, _spell, _teemo, _teemo, infiniteduration: true);
        _stealthTimerEnable = false;
    }

    private void OnTakeDamage(DamageData data)
    {
        _stealthTimerEnable = false;
        _periodicTicker.Reset();
    }

    private void OnSpellsCast(Spell spell) { BreakStealth(); }

    private void BreakStealth() {
        _stealthTimerEnable = false;
        _periodicTicker.Reset();
        if (!_teemo.HasBuff("CamouflageStealth")) return;
        RemoveBuff(_teemo, "CamouflageStealth");
        AddBuff("CamouflageBuff", 3f, 1, _spell, _teemo, _teemo);
    }
}
