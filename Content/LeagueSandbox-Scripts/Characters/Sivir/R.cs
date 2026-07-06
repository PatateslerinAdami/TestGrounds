using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SivirR : ISpellScript
{
    private ObjAIBase _sivir;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _sivir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
    }

    public void OnSpellPostCast(Spell spell)
    {
        var units = ForEachUnitInTargetArea(_sivir, _sivir.Position, 1000f,
            SpellDataFlags.AffectFriends | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var unit in units)
        {
            SpellCast(_sivir, 5, SpellSlotType.ExtraSlots, true, unit, Vector2.Zero);
        }
    }
}

public class SivirRMissile : ISpellScript
{
    private ObjAIBase _sivir;
    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Target
        },
        IsDamagingSpell = false,
        
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _sivir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        //AddBuff
    }
}