using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class BloodBoil : ISpellScript {
    private ObjAIBase _nunu;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nunu = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
    }

    public void OnSpellCast(Spell spell)
    {
        AddBuff("BloodBoil", 12f, 1, spell, _target, _nunu);
        if (_target == _nunu)
        {
             var unit = GetUnitsInRange(_nunu, _nunu.Position, 700f, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.NotAffectSelf)
                .OrderBy(unit => unit is Champion).First();
             if (unit == null) return;
             AddBuff("BloodBoil", 12f, 1, spell, unit, _nunu);
        }
        else
        {
            AddBuff("BloodBoil", 12f, 1, spell, _nunu, _nunu);
        }
    }
}