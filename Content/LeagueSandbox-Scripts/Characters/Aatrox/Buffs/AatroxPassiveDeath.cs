using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class AatroxPassiveDeath : IBuffGameScript {
    private       ObjAIBase _aatrox;
    private       Spell     _spell;
    private       float     _tickTimer = 500f;
    private const float     TickTime   = 500f;
    private       short     _stepCount = 0;
    private const short     MaxSteps   = 6;
    private       float     _blood = 0f;
    private       float     _heal      = 0f;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _aatrox = ownerspell.CastInfo.Owner;
        _spell  = ownerspell;
        _blood  = _aatrox.GetPAR() / 6f;
        _heal   = _aatrox.GetMaxPAR() * 0.35f / 6f + _blood;
        unit.StopMovement();
        _aatrox.SetTargetUnit(null, true);
        SetStatus(_aatrox, StatusFlags.Targetable,   false);
        SetStatus(_aatrox,       StatusFlags.Invulnerable, true);
        SetStatus(_aatrox, StatusFlags.CanMove,       false);
        SetStatus(_aatrox, StatusFlags.CanAttack,   false);
        SetStatus(_aatrox, StatusFlags.Stunned,     true);
        _aatrox.Stats.CurrentHealth = 1f;
        var passiveActivateParticle = _aatrox.SkinID switch {
            1 => "Aatrox_Skin01_Passive_Death_Activate",
            2 => "Aatrox_Skin02_Passive_Death_Activate",
            _ => "Aatrox_Base_Passive_Death_Activate"
        };
        AddParticleTarget(_aatrox, _aatrox, passiveActivateParticle, _aatrox, buff.Duration);
        PlayAnimation(_aatrox,"Passive_Death", 3f);
    }

    public void OnUpdate(float diff) {
        if (_stepCount >= MaxSteps) return;
        _tickTimer += diff;
        if (_tickTimer < TickTime) return;
        _aatrox.TakeHeal(_aatrox, _heal, HealType.SelfHeal);
        _aatrox.SpendPAR(_blood);
        _tickTimer = 0f;
        _stepCount++;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        SetStatus(_aatrox, StatusFlags.CanMove,   true);
        SetStatus(_aatrox,       StatusFlags.Invulnerable, false);
        SetStatus(_aatrox, StatusFlags.Targetable, true);
        SetStatus(_aatrox, StatusFlags.CanAttack, true);
        SetStatus(_aatrox, StatusFlags.Stunned,   false);
        var passiveEndParticle = _aatrox.SkinID switch {
            1 => "Aatrox_Skin01_Passive_Death_End",
            2 => "Aatrox_Skin02_Passive_Death_End",
            _ => "Aatrox_Base_Passive_Death_End"
        };
        AddParticleTarget(_aatrox, _aatrox, passiveEndParticle, _aatrox);
        
        float duration = _aatrox.Stats.Level switch {
            <6  => 225f,
            <11 => 200f,
            <16 => 175f,
            >=16 => 150f
        };
        spell.SetCooldown(10f, true);
        SetPARState(_aatrox, 1);
        AddBuff("AatroxPassiveActivate", 10f, 1, _spell, _aatrox, _aatrox);
    }
}
