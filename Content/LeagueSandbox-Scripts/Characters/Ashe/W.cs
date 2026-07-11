using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
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

public class Volley : ISpellScript {
    private ObjAIBase _ashe;
    private Vector2 _endPos;
    private Vector2 _startPos;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true
    };

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _ashe = owner;
        _startPos = _ashe.Position;
        _endPos = end;
    }

    public void OnSpellPostCast(Spell spell) {
        
        var toTarget = _endPos - _startPos;
        if (toTarget.LengthSquared() <= 0.0001f) return;

        var baseDir = Vector2.Normalize(toTarget);
        var distance = toTarget.Length();
        
        spell.CastInfo.InstanceVars.Set("volleyHits", new HashSet<AttackableUnit>());

        for (var i = 0; i < 8; i++) {
            var angle = -21f + 7f * i;
            var dir = GameServerCore.Extensions.Rotate(baseDir, angle);
            var pos = _startPos + dir * distance;

            SpellCast(_ashe, 0, SpellSlotType.ExtraSlots, pos, pos, true, Vector2.Zero,
                      inheritVariablesFrom: spell.CastInfo);
        }
    }
}

public class VolleyAttack : ISpellScript {
    private ObjAIBase _ashe;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Arc,
        },
        NotSingleTargetSpell = true,
        TriggersSpellCasts = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ashe = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        var hits = missile.CastInfo.InstanceVars.Get<HashSet<AttackableUnit>>("volleyHits");
        if (hits == null || !hits.Add(target)) {
            return;
        }
        
        var particleName = _ashe.SkinID switch
        {
            6 => "Ashe_Skin06_W_tar.troy",
            _ => "Ashe_Base_W_tar.troy"
        };
        SpellEffectCreate(particleName, _ashe, target, target, scale: 1f, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
        
        var qSpell = _ashe.GetSpell("FrostShot");
        if (qSpell.CastInfo.SpellLevel >= 1) {
            AddBuff("FrostArrow", 2f, 1, qSpell, target, _ashe);
        }
        
        var mainSpell = _ashe.GetSpell("Volley");
        var ad = _ashe.Stats.AttackDamage.Total;
        var dmg = mainSpell.SpellData.EffectLevelAmount[2][mainSpell.CastInfo.SpellLevel] + ad;
        target.TakeDamage(_ashe, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                          DamageResultType.RESULT_NORMAL);
        
        missile.SetToRemove();
    }
}