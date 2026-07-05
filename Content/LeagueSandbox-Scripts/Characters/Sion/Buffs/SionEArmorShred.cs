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

internal class SionEArmorShred : IBuffGameScript {
    private       ObjAIBase      _sion;
    private Particle _p1;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SHRED,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _sion = ownerSpell.CastInfo.Owner;
        _p1 = AddParticleTarget(_sion, unit, "Global_Debuff_ArmorPen.troy", unit, lifetime: buff.Duration, flags: FXFlags.SimulateWhileOffScreen);
        StatsModifier.Armor.PercentBonus -= 0.2f;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p1);
    }
}