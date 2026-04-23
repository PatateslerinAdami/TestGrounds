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
    private ObjAIBase                                _owner;
    private Spell                                    _spell;
    private readonly HashSet<AttackableUnit> _affectedUnits = new();


    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        _spell = spell;
    }

    public void OnUpdate(float diff)
    {
        //maybe periodic check like 0.25s?
        var unitsInRange = GetUnitsInRange(_owner, _owner.Position, 800f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)
                           .ToHashSet();
        
        foreach (var unit in unitsInRange)
        {
            if (_affectedUnits.Add(unit))
            {
                AddBuff("Dread", 1500f, 1, _spell, unit, _owner, infiniteduration: true);
            }
        }
        
        foreach (var unit in _affectedUnits.Except(unitsInRange).ToList())
        {
            RemoveBuff(unit, "Dread");
            _affectedUnits.Remove(unit);
        }
    }
}