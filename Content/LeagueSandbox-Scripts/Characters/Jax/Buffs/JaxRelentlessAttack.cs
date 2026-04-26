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

public class JaxRelentlessAttack  : IBuffGameScript {
    private ObjAIBase _jax;
    private bool      _pendingAutoAttackOverride;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _jax = ownerspell.CastInfo.Owner;
        _jax.SetAutoAttackSpell("JaxRelentlessAttack", false);
    }

    public void OnUpdate(float diff) {
        if (!_pendingAutoAttackOverride) return;
        TryApplyAutoAttackOverride();
    }

    private void TryApplyAutoAttackOverride() {
        if (_jax.IsAttacking) {
            _pendingAutoAttackOverride = true;
            return;
        }

        _pendingAutoAttackOverride = false;
        _jax.SetAutoAttackSpell("JaxRelentlessAttack", false);
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        _jax.ResetAutoAttackSpell();
    }
    
}
