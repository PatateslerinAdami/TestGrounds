using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SivirQ : ISpellScript {
    private ObjAIBase _sivir;
    private Vector2 _start, _end;

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
        SpellDamageRatio = 0.5f
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _sivir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _start = start;
        _end = end;
    }

    public void OnSpellPostCast(Spell spell)
    {
        SpellCast(_sivir, 1, SpellSlotType.ExtraSlots, _start, _end, true, Vector2.Zero);
    }
}

public class SivirQMissile : ISpellScript {
    
    private ObjAIBase _sivir;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc
        },
        NotSingleTargetSpell = true,
        TriggersSpellCasts = false,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
        SpellDamageRatio = 0.5f,
        PersistsThroughDeath = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _sivir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
    }

    private void OnSpellMissileEnd(SpellMissile missile)
    {
        SpellCast(_sivir, 3, SpellSlotType.ExtraSlots, false, _sivir, missile.Position);
        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnSpellMissileEnd);
        ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.RemoveListener(this, _spell, OnSpellHit);
    }
}

public class SivirQMissileReturn : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc
        },
        NotSingleTargetSpell = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        SpellDamageRatio = 0.5f
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {

    }
}

public class SivirQMissileReturnDead : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {

    }
}