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

public class JaxRelentlessAssaultSpeed  : IBuffGameScript {
    private ObjAIBase _jax;
    private Buff      _buff;
    private Particle  _p1;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
            PersistsThroughDeath = true,
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _buff                               = buff;
        _jax                                = buff.SourceUnit;
        var armorPerBonusAd = _jax.Stats.AttackDamage.FlatBonus * 0.3f;
        var magicResistPerBonusAp = _jax.Stats.AbilityPower.Total * 0.2f;
        var bonusArmor = ownerspell.SpellData.EffectLevelAmount[3][ownerspell.CastInfo.SpellLevel] + armorPerBonusAd;
        var bonusMagicResist = ownerspell.SpellData.EffectLevelAmount[3][ownerspell.CastInfo.SpellLevel] + magicResistPerBonusAp;
        StatsModifier.Armor.FlatBonus       = bonusArmor;
        StatsModifier.MagicResist.FlatBonus = bonusMagicResist;
        unit.AddStatModifier(StatsModifier);
        _p1 = AddParticleTarget(_jax, _jax, "JaxRelentlessAssault_buf", _jax, buff.Duration);
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        RemoveParticle(_p1);
    }
    
}