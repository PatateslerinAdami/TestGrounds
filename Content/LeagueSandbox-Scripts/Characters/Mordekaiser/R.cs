using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiEventManager;

namespace Spells;

public class MordekaiserChildrenOfTheGrave : ISpellScript
{
    private Particle _p;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        OnPreDamagePriority = 10
    };

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        var buff = AddBuff("MordekaiserCOTGDot", 10.400024f, 1, spell, target, owner);
        AddBuff("MordekaiserChildrenOfTheGrave", 10.400024f, 1, spell, target, owner);
        OnBuffDeactivated.AddListener(this, buff, OnBuffRemoved, true);
    }

    public void OnBuffRemoved(Buff buff)
    {
        RemoveParticle(_p);
    }
}

//Target Revive
public class MordekaiserCOTGRevive : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
    };
}

//Unit Controller
public class MordekaiserCotGGuide : BasePetController
{
}