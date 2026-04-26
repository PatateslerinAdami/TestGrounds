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

namespace ItemPassives;

public class ItemID_3117 : IItemScript {
    private ObjAIBase      _owner;
    public  StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage);
        AddBuff("BootsOfMobilityBuff", 25000f, 1, _owner.AutoAttackSpell, _owner, _owner, true);
    }
    private void OnTakeDamage(DamageData data) {
        AddBuff("BootsOfMobilityDebuff", 5f, 1, _owner.AutoAttackSpell, _owner, _owner);
    }
}