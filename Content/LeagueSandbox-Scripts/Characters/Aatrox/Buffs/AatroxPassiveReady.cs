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

public class AatroxPassiveReady : IBuffGameScript {
    private ObjAIBase _aatrox;
    private Spell     _spell;
    private Buff      _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _aatrox = ownerspell.CastInfo.Owner;
        _spell  = ownerspell;
        _buff   = buff;
        SetPARState(_aatrox, 0);
        ApiEventManager.OnTakeDamage.AddListener(this, _aatrox, OnTakeDamage);
    }

    private void OnTakeDamage(DamageData data)
    {
        if (!(_aatrox.Stats.CurrentHealth <= data.PostMitigationDamage)) return;
        data.PostMitigationDamage = 0f;
        AddBuff("AatroxPassiveDeath", 3f, 1, _spell, _aatrox, _aatrox);
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) { }
}
