using System.Numerics;
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
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class Volley : ISpellScript {
    private ObjAIBase _ashe;
    private Vector2 _endPos;
    private Vector2 _startPos;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
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

        for (var i = 0; i < 8; i++) {
            var angle = -21f + 7f * i;
            var dir = GameServerCore.Extensions.Rotate(baseDir, angle);
            var pos = _startPos + dir * distance;

            SpellCast(_ashe, 0, SpellSlotType.ExtraSlots, pos, pos, true, Vector2.Zero);
        }
    }
}

public class VolleyAttack : ISpellScript {
    private ObjAIBase _ashe;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Arc,
        },
        TriggersSpellCasts = false,
        DoesntBreakShields = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ashe = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        if (!target.HasBuff("VolleyAttack")) {
            if (_ashe.GetSpell("FrostShot").CastInfo.SpellLevel >= 1) {
                AddBuff("FrostArrow", 2f, 1, spell, target, _ashe);
            }

            AddBuff("VolleyAttack", 0.5f, 1, spell, target, _ashe);
            var dmg = 40 + 10 * (_ashe.GetSpell("Volley").CastInfo.SpellLevel - 1) + _ashe.Stats.AttackDamage.Total;
            AddParticleTarget(_ashe, target, "Ashe_Base_W_tar", target);
            target.TakeDamage(_ashe, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                              DamageResultType.RESULT_NORMAL);
        }

        missile.SetToRemove();
    }
}