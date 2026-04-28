using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class UdyrTigerPunch : IBuffGameScript {
    private ObjAIBase _udyr;
    private Particle  _particle, _particle2, _particle3;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell spell) {
        _udyr = spell.CastInfo.Owner;
        StatsModifier.AttackSpeed.PercentBonus = spell.CastInfo.SpellLevel switch {
            1 => 0.30f,
            2 => 0.40f,
            3 => 0.50f,
            4 => 0.60f,
            5 => 0.70f,
            _ => 0.30f
        };
        _udyr.AddStatModifier(StatsModifier);
        _particle = AddParticleTarget(_udyr, _udyr, "TigerStance", _udyr, 2f, size: _udyr.Stats.Size.Total);
        _particle2 = AddParticleTarget(_udyr, _udyr, "Udyr_Tiger_buf", _udyr, 5f, size: _udyr.Stats.Size.Total,
                                       bone: "L_Finger");
        _particle3 = AddParticleTarget(_udyr, _udyr, "Udyr_Tiger_buf_R", _udyr, 5f, size: _udyr.Stats.Size.Total,
                                       bone: "R_Finger");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        RemoveParticle(_particle);
        RemoveParticle(_particle2);
        RemoveParticle(_particle3);
    }
}
