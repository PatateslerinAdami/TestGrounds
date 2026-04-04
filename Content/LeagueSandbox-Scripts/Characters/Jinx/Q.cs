using System;
using System.Collections.Generic;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class JinxQ : ISpellScript {
    private  ObjAIBase _jinx;
    

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jinx  = owner;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        if (_jinx.HasBuff("JinxQIcon")) {
            RemoveBuff(_jinx, "JinxQIcon");
            AddBuff("JinxQ", 25000f ,1, spell, _jinx, _jinx, true);
        } else if (_jinx.HasBuff("JinxQ")) {
            RemoveBuff(_jinx, "JinxQ");
            AddBuff("JinxQIcon", 25000f, 1, spell, _jinx, _jinx, true);
        }
    }

    private void OnLevelUpSpell(Spell spell) {
        if (spell.CastInfo.SpellLevel != 1) return;
        AddBuff("JinxQIcon", 25000f, 1, spell, _jinx, _jinx, true);
    } 
}

public class JinxQAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        IsDamagingSpell = true
    };
}

public class JinxQAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        IsDamagingSpell = true
    };
}

public class JinxQCritAttack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        IsDamagingSpell = true
    };
}
