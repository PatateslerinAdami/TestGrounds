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

internal class SionPassive : IBuffGameScript
{
    private ObjAIBase _sion;
    private AttackableUnit _unit;
    private Spell _ownerSpell;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        PersistsThroughDeath = true,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _sion = buff.SourceUnit;
        _unit = unit;
        _ownerSpell = ownerSpell;
        ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
        ApiEventManager.OnZombie.AddListener(this, unit, OnZombie);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.OnDeath.RemoveListener(this);
        ApiEventManager.OnZombie.RemoveListener(this);
    }
    
    private void OnDeath(DeathData data) {
        if (_unit is Champion) {
            data.BecomeZombie = true;
        }
    }
    
    private void OnZombie(AttackableUnit unit, DeathData data) {
        AddBuff("SionPassiveDelay", 1.5f, 1, _ownerSpell, unit, _sion);
    }
}
