using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class UdyrPhoenixStance : ISpellScript {
    private ObjAIBase _owner;
    private Spell     _spell;

    public StatsModifier StatsModifier { get; } = new();

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _spell = spell;
        _owner = owner;
        //change per level
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        AddBuff("UdyrPhoenixStance",     25000f, 1, spell, owner, owner, true);
        AddBuff("UdyrPhoenixActivation", 5f,     1, spell, owner, owner);
    }
}

public class UdyrPhoenixMissile : ISpellScript {
    private ObjAIBase _udyr;
    private Spell     _spell;

    public StatsModifier StatsModifier { get; } = new();

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Circle
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _spell = spell;
        _udyr  = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        ScriptMetadata.MissileParameters.OverrideEndPosition = end;
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!IsValidTarget(_udyr, target,
                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                           SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        var dmg = 40f + 40f                      * (_udyr.GetSpell("UdyrPhoenixStance").CastInfo.SpellLevel - 1) +
                  _udyr.Stats.AbilityPower.Total * 0.45f;
        target.TakeDamage(_udyr, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                          DamageResultType.RESULT_NORMAL);
    }
}

public class UdyrPhoenixAttack : ISpellScript {
    private ObjAIBase _udyr;

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _udyr = owner; }
}