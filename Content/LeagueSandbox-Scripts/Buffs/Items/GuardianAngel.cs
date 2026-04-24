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

internal class GuardianAngel : IBuffGameScript
{

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.INTERNAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        PlayAnimation(unit, "Death", 4f);
        SetStatus(unit, StatusFlags.CanAttack, false);
        SetStatus(unit, StatusFlags.CanMove, false);
        SetStatus(unit, StatusFlags.CanCast, false);
        SetStatus(unit, StatusFlags.Targetable, false);
        SetStatus(unit, StatusFlags.Invulnerable, true);
        SetStatus(unit, StatusFlags.Stunned, true);
        unit.StopMovement();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        SetStatus(unit, StatusFlags.CanAttack, true);
        SetStatus(unit, StatusFlags.CanMove, true);
        SetStatus(unit, StatusFlags.CanCast, true);
        SetStatus(unit, StatusFlags.Targetable, true);
        SetStatus(unit, StatusFlags.Invulnerable, false);
        SetStatus(unit, StatusFlags.Stunned, false);
        AddBuff("HasBeenRevived", 300f, 1, ownerSpell, unit, null);
        AddParticleTarget(unit, unit, "GuardianAngel_tar.troy", unit);
        AddParticleTarget(unit, unit, "GuardianAngel_cas.troy", unit);
    }
}



