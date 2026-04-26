using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace ItemPassives;

public class ItemID_3302 : IItemScript
{
    private ObjAIBase _owner;
    public StatsModifier StatsModifier { get; } = new();
    private const float RECHARGE_PERIOD              = 60f;
    private const byte MAX_STACKS                    = 2;

    public void OnActivate(ObjAIBase owner)
    {
        _owner = owner;

        BuffVariables payload = new BuffVariables();
        payload.Set("itemid", 3302);
        AddBuff("TalentReaper", RECHARGE_PERIOD, MAX_STACKS, _owner.AutoAttackSpell, _owner, _owner, true, buffVariables: payload);
    }




}