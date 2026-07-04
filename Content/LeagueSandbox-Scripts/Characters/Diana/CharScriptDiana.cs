using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptDiana : ICharScript {
    private ObjAIBase _diana;
    private Spell _spell;
    private int _counter = 0;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _diana = owner;
        _spell = spell;
        ApiEventManager.OnHitUnit.AddListener(this, _diana, OnHit);
    }

    public void OnPostActivate(ObjAIBase owner, Spell spell = null)
    {
        AddBuff("DianaCombatBuff", 250000f, 1, _spell, _diana, _diana, true);
    }

    private void OnHit(DamageData data)
    {
        _counter++;
        LogDebug("" + _counter);
        switch (_counter)
        {
            case 2:
                AddBuff("DianaPassive", 4f, 1, _spell, _diana, _diana);
                break;
            case 3:
                _counter = 0;
                break;
        }
    }
}