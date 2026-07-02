
using Perfetto.Protos;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
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

namespace CharScripts;

public class CharScriptMasterYi : ICharScript
{
    private ObjAIBase _masterYi;
    private int _hitCounter = 0;

    public void OnActivate(ObjAIBase owner, Spell spell = null)
    {
        _masterYi = owner;
        ApiEventManager.OnHitUnit.AddListener(this, _masterYi, OnHit);
    }

    private void OnHit(DamageData data)
    {
        // Count only genuine basic attacks — an on-hit spell like Alpha Strike deals
        // DAMAGE_SOURCE_ATTACK (so it procs on-hit effects) but must NOT advance Double Strike.
        if (!data.IsAutoAttack) return;
        if (_masterYi.HasBuff("DoubleStrikeReady") || _masterYi.HasBuff("DoubleStrike"))return;
        AddBuff("DoubleStrikeStacks", 4f, 1, _masterYi.AutoAttackSpell, _masterYi, _masterYi);
        if (_masterYi.GetBuffsWithName("DoubleStrikeStacks").Count != 3) return;
        AddBuff("DoubleStrikeReady", 4f, 1, _masterYi.AutoAttackSpell, _masterYi, _masterYi);
    }
}
