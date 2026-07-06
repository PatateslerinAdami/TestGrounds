using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class AhriSeduce : IBuffGameScript {
    private       ObjAIBase      _ahri;
    private       AttackableUnit _unit;
    private       Particle       _charmParticle;
    private const float          _slowPercentage = 0.4f;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.CHARM,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ahri = ownerSpell.CastInfo.Owner;
        _unit = unit;

        // Charmed is DERIVED from BuffType.CHARM (AttackableUnit.RecomputeBuffEffects) — overlap-safe.
        // Charm = AI-driven pull toward the charmer (Ahri). The shared CrowdControlComponent reads
        // CrowdControlSource and walks the unit toward Ahri WITHOUT attacking, re-pathing to Ahri's
        // current position (see CrowdControlComponent.OnCharmBegin). Charm only targets champions /
        // minions, which all run an AI crowd-control driver, so no legacy buff-driven fallback.
        if (unit is ObjAIBase cc) {
            cc.CrowdControlSource = _ahri;
        }

        _charmParticle = AddParticleTarget(_ahri, unit, "Ahri_Charm_tar.troy", unit, buff.Duration);

        StatsModifier.MoveSpeed.PercentBonus = -_slowPercentage;
        unit.AddStatModifier(StatsModifier);

        ApplyAssistMarker(unit, _ahri, 10.0f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (_charmParticle != null) {
            _charmParticle.SetToRemove();
        }

        if (unit is ObjAIBase cc) {
            cc.CrowdControlSource = null;
        }
        unit.StopMovement();
    }
}