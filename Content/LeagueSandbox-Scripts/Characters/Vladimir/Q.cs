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

public class VladimirTansfusion : ISpellScript
{
    private ObjAIBase _vladimir;
    private AttackableUnit _target;
    

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
        _target = target;
    }

    public void OnSpellPostCast(Spell spell)
    {
        var ap =  _vladimir.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
        _target.TakeDamage(_vladimir, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
            DamageResultType.RESULT_NORMAL);
    }
}

public class VladimirTansfusionHeal : ISpellScript
{
    private ObjAIBase _vladimir;
        

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _vladimir = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }
    
    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var transfusion = _vladimir.GetSpell("VladimirTransfusion");
        var ap = _vladimir.Stats.AbilityPower.Total * transfusion.SpellData.Coefficient2;
        var heal = transfusion.SpellData.EffectLevelAmount[1][transfusion.CastInfo.SpellLevel] + ap;
        target.TakeHeal(_vladimir, heal, HealType.SelfHeal);
    }
}
