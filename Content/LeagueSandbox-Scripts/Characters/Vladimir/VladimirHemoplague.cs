using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class VladimirHemoplague : ISpellScript
{
    private ObjAIBase _vladimir;
    private Vector2 _end;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _vladimir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _end = end;
    }

    public void OnSpellPostCast(Spell spell)
    {
        //GetChampionsInRange(_vladimir, _end,);
    }
}

public class VladimirHemoplagueMissile : ISpellScript
{
    private ObjAIBase _vladimir;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = false,
        IsDamagingSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _vladimir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        var mainSpell = _vladimir.GetSpell("VladimirHemoplague");
        var ap = _vladimir.Stats.AbilityPower.Total * mainSpell.SpellData.Coefficient;
        var dmg = mainSpell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
    }
}
