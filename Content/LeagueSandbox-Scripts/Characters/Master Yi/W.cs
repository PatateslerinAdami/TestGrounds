using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class Meditate : ISpellScript
{
    private ObjAIBase _masterYi;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        TriggersSpellCasts = true,
        ChannelDuration = 4,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _masterYi = owner;
    }

    public void OnSpellPostCast(Spell spell)
    {
    }

    public void OnSpellChannel(Spell spell)
    {
        AddBuff("Meditate", 4f, 1, spell, _masterYi, _masterYi);
    }

    public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
    {
        RemoveBuff(_masterYi, "Meditate");
    }

    public void OnSpellPostChannel(Spell spell)
    {
        RemoveBuff(_masterYi, "Meditate");
    }
}