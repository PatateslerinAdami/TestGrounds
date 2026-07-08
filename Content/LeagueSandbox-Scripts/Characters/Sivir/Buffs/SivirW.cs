using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class SivirW : IBuffGameScript {
    private ObjAIBase _sivir;
    private Spell     _spell;
    private Buff _buff;
    private Particle  _p1;
    private int _hitCount = 0;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _sivir = buff.SourceUnit;
        _spell = ownerSpell;
        _buff = buff;
        _hitCount = 0;
        _sivir.SetAutoAttackSpell("SivirWAttack", true);
        ApiEventManager.OnLaunchAttack.AddListener(this, _sivir, OnLaunchAttack);
    }

    private void OnLaunchAttack(Spell spell) {
        if (!spell.CastInfo.IsAutoAttack) return;
        _hitCount++;
        if (_hitCount >= 3) _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_p1);
        _sivir.ResetAutoAttackSpell();
    }
}
