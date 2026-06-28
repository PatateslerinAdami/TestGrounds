using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class BlindingDart : ISpellScript {
    private ObjAIBase _teemo;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        TriggersSpellCasts   = true,
        NotSingleTargetSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _teemo = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile) {
        var ap     = _teemo.Stats.AbilityPower.Total * 0.8f;
        var damage = 80f  + 45f + (spell.CastInfo.SpellLevel - 1) + ap;
        var duration   = 1.5f + 0.25f * (spell.CastInfo.SpellLevel - 1);
        AddBuff("Blind", duration, 1, spell, target, _teemo);
        target.TakeDamage(_teemo, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
    }
}