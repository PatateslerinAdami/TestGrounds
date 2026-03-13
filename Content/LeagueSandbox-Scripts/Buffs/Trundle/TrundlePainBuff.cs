using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class TrundlePainBuff : IBuffGameScript {
    private AttackableUnit _unit;
    private Particle       _p1, _p2, _p3;
    private float          _currentArmorBonus;
    private float          _currentMagicResistBonus;
    
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit = unit;
        _currentArmorBonus = 0f;
        _currentMagicResistBonus = 0f;
        _p1 = AddParticleTarget(unit, unit, "Trundle_R_Grow", unit, buff.Duration);  
        _p2 = AddParticleTarget(unit, unit, "TrundleUlt_self_buf", unit, buff.Duration);  
        _p3 = AddParticleTarget(unit, unit, "Trundle_R_Glowy_Eyes", unit, buff.Duration, bone: "head");  
    }

    public void ApplyBonusDelta(float armorDelta, float magicResistDelta) {
        if (_unit == null || _unit.IsDead) return;
        
        _currentArmorBonus += armorDelta;
        _currentMagicResistBonus += magicResistDelta;

        _unit.RemoveStatModifier(StatsModifier);
        StatsModifier.Armor.FlatBonus = _currentArmorBonus;
        StatsModifier.MagicResist.FlatBonus = _currentMagicResistBonus;
        _unit.AddStatModifier(StatsModifier);
    }
}