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

public class AhriSeduce : ISpellScript {
    private  ObjAIBase _ahri;
    private Vector2 _end;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        NotSingleTargetSpell = true,
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ahri = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpelHit);
    }

    private void OnSpelHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        AddBuff("AhriSeduce", spell.SpellData.EffectLevelAmount[2][spell.CastInfo.SpellLevel], 1, spell, target, _ahri);
        
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _end = end;
    }

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_ahri, 4, SpellSlotType.ExtraSlots, _end, _end, true, Vector2.Zero);
    }
}

public class AhriSeduceMissile : ISpellScript {
    private  ObjAIBase _ahri;
    private Vector2 _end;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc
        },
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ahri = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        _ahri.Spells[2].ApplyEffects(target);
        missile.SetToRemove();
    }
}

