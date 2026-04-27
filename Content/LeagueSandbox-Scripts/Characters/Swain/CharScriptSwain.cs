using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptSwain : ICharScript
{
    private ObjAIBase _swain;

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _swain = owner;
        ApiEventManager.OnKill.AddListener(this, owner, OnKill);
        ApiEventManager.OnAssist.AddListener(this, owner, OnAssist);
    }

    private void OnKill(DeathData data)
    {
        if (data.Unit is Champion)
        {
            _swain.Stats.CurrentMana += _swain.Stats.ManaPoints.Total * 0.09f;
        }
        else
        {
            _swain.Stats.CurrentMana += 13 + 1 * (_swain.Stats.Level - 1);
        }
    }

    private void OnAssist(ObjAIBase assistant, DeathData data)
    {
        if (data.Unit is Champion)
        {
            _swain.Stats.CurrentMana += _swain.Stats.ManaPoints.Total * 0.09f;
        }
    }
}