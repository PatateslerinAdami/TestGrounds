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

internal class DarkBinding : IBuffGameScript {
    private ObjAIBase        _morgana;
    private Particle _snareParticle;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SNARE,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _morgana = ownerSpell.CastInfo.Owner;
        _snareParticle = AddParticleTarget(_morgana, unit, "Morgana_Base_Q_Tar", unit, buff.Duration);
        SetStatus(unit, StatusFlags.Rooted, true);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        SetStatus(unit, StatusFlags.Rooted, false);
        RemoveParticle(_snareParticle);
    }
}