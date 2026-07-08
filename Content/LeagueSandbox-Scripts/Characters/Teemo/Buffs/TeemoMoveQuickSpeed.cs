using System.Threading;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class TeemoMoveQuickSpeed : IBuffGameScript {
    private ObjAIBase _teemo;
    private Particle  _hasteParticle;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HASTE,
        BuffAddType = BuffAddType.RENEW_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _teemo = buff.SourceUnit;
        var spellLevel = ownerSpell.CastInfo.SpellLevel;
        StatsModifier.MoveSpeed.PercentBonus += spellLevel switch {
            1 => 0.10f,
            2 => 0.14f,
            3 => 0.18f,
            4 => 0.22f,
            5 => 0.26f,
            _ => 0.10f
        };
        _hasteParticle = AddParticleTarget(_teemo, _teemo, "MoveQuick_buf.troy",
                                           unit,
                                           buff.Duration, bone: "BUFFBONE_GLB_GROUND_LOC");
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { RemoveParticle(_hasteParticle); }
}