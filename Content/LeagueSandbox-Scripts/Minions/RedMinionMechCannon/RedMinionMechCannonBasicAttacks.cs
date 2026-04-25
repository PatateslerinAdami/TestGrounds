using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

public class Red_Minion_MechCannonBasicAttack : ISpellScript {
    private ObjAIBase      _owner;

    public SpellScriptMetadata ScriptMetadata => new() {
        
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        IsDamagingSpell      = true,
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
    }
}

public class Red_Minion_MechCannonBasicAttack2 : ISpellScript {
    private ObjAIBase      _owner;

    public SpellScriptMetadata ScriptMetadata => new() {
        
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        IsDamagingSpell      = true,
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        
    }
}