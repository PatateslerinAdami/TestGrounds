using System.Numerics;
using Buffs;
using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

public class TalonCutthroat : ISpellScript {
    private ObjAIBase _talon;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts    = true,
        IsDamagingSpell       = true,
        OverrideCooldownCheck = true,
        CastingBreaksStealth  = true,
        NotSingleTargetSpell  = false,
        IsDeathRecapSource    = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        _spell = spell;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        AddParticleTarget(owner, null, "talon_E_cast", owner);

        if (target.Team == owner.Team) return;


        //PlayAnimation(owner, "Spell3");

        //blink 
        var coords = CalcVector(180.0F, owner.Position, target.Position);
        TeleportTo(owner, coords.X, coords.Y);
        FaceDirection(target.Position, owner, true);

        //slow
        var variables      = new BuffVariables();
        variables.Set("slowAmount", 0.99f);
        AddBuff("Slow", 0.25f, 1, spell, target, owner, buffVariables: variables);
        
        AddParticleTarget(owner, target, "talon_E_tar", target);
        
        //dmg amp
        AddBuff("TalonDamageAmp", 3f, 1, spell, target, owner);
        _talon.SetTargetUnit(target, true);
    }

    private static Vector2 CalcVector(in float distance, in Vector2 player, in Vector2 target) {
        return target - (player - target).Normalized() * (!IsWalkable(target.X, target.Y) ? -distance : distance);
    }
}
