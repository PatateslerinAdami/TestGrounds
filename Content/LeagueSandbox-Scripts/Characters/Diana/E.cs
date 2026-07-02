using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class DianaVortex : ISpellScript {
    private  ObjAIBase _diana;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _diana = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        AddParticleTarget(_diana, _diana, "Diana_Base_E_Precas.troy", _diana);
    }

    public void OnSpellPostCast(Spell spell)
    {
        AddParticleTarget(_diana, _diana, "Diana_Base_E_Cas.troy", _diana);
        var unitsInRange = GetUnitsInRange(_diana, _diana.Position, 450f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectHeroes);

        foreach (var unit in unitsInRange)
        {
            AddBuff("DianaVortexStun", 0.5f, 1, spell, unit, _diana);
        }
    }
}
