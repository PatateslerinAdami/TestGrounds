using System.Numerics;
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

internal class Pantheon_GrandSkyfall : IBuffGameScript
{
    private ObjAIBase _pantheon;
    private float _tickTimer;
    private bool _isActive = false;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _pantheon = ownerSpell.CastInfo.Owner;
        _tickTimer = 150f;
        _isActive = true;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
    }

    public void OnUpdate(float diff)
    {
        if (!_isActive) return;
        _tickTimer -= diff;
        if (_tickTimer > 0) return;
        _pantheon.SetStatus(StatusFlags.CanAttack, false);
        _pantheon.SetStatus(StatusFlags.Targetable, false);
        _pantheon.SetStatus(StatusFlags.NoRender, true);
        _pantheon.SetStatus(StatusFlags.Invulnerable, true);
        _isActive = false;
    }
}