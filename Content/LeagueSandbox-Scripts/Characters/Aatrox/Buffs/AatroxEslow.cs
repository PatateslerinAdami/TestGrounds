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

internal class AatroxEslow : IBuffGameScript {
    private       ObjAIBase      _aatrox;
    private       AttackableUnit _unit;
    private       Particle       _slow, _slow2;
    private const float          _slowPercentage = 0.4f;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _aatrox = ownerSpell.CastInfo.Owner;
        _unit  = unit;
        
        _slow  = AddParticleTarget(_aatrox, null, "Global_Slow", unit, buff.Duration, bone: "BUFFBONE_GLB_GROUND_LOC");
        switch (_aatrox.SkinID) {
            
        }
        _slow2  = AddParticleTarget(_aatrox, unit, "Aatrox_Base_E_Slow", unit);
        //ApplyAssistMarker(unit, _aatrox, 10.0f);
        
        StatsModifier.MoveSpeed.PercentBonus -= _slowPercentage;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_slow);
        RemoveParticle(_slow2);
    }
}