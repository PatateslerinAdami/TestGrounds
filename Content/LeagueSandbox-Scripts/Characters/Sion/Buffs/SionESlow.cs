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

internal class SionESlow : IBuffGameScript {
    private       ObjAIBase      _sion;
    private Particle _p1;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _sion = buff.SourceUnit;
        _p1 = AddParticleTarget(_sion, unit, unit is Champion ? "sion_base_e_buf_champ.troy" : "Sion_Base_E_Buf.troy", unit, lifetime: buff.Duration, flags: FXFlags.SimulateWhileOffScreen | FXFlags.PARDriven);
        StatsModifier.MoveSpeed.PercentBonus -= ownerSpell.SpellData.EffectLevelAmount[1][ownerSpell.CastInfo.SpellLevel]/100;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p1);
    }
}