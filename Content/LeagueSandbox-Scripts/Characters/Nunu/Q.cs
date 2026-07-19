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

public class Consume : ISpellScript {
    private ObjAIBase _nunu;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        NotSingleTargetSpell = false,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nunu = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
    }

    public void OnSpellPostCast(Spell spell)
    {
        
        var dmg            = _target.HasBuff("ResistantSkin") ? spell.SpellData.EffectLevelAmount[3][spell.CastInfo.SpellLevel] : spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel];
        var ap = _nunu.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var heal = spell.SpellData.EffectLevelAmount[2][spell.CastInfo.SpellLevel] + ap;
        SpellEffectCreate("yeti_Consume_tar.troy", _nunu, _target, null, keywordObject: _nunu, flags: FXFlags.UpdateOrientation);
        _target.TakeDamage(_nunu, dmg, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_SPELL, false);
        SpellEffectCreate("Meditate_eff.troy", _nunu, _nunu, _nunu, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 0f);
        _nunu.TakeHeal(_nunu, heal, HealType.SelfHeal);
        if (_target is not Monster monster || !_target.IsDead) return;
        // Match on Model, NOT Name: jungle monsters are spawned as CreateJungleMonster(name, model, ...)
        // where name is unique ("SRU_Blue1.1.1") and model is the camp type ("SRU_Blue"). monster.Name
        // therefore never equals "SRU_Blue" — the switch has to use monster.Model.
        var buffName = monster.Model switch
        {
            // Patch 4.20 Consume categories (LoL wiki), Map11 SRU camp models:
            // Rough Rock Candy (size + health + heal): Blue Sentinel/Sentry, Red Brambleback/
            // Cinderling, Ancient Krug/Krug.
            "SRU_Blue" or "SRU_BlueMini" or "SRU_BlueMini2"
                or "SRU_Red" or "SRU_RedMini"
                or "SRU_Krug" or "SRU_KrugMini" => "NunuQBuffGolem",
            // Ornery Monster Tails (bonus magic damage on attacks/abilities): Baron, Dragon,
            // Gromp, Vilemaw. (4.20 moved Gromp here and Red out to Rough Rock Candy.)
            "SRU_Baron" or "SRU_Dragon" or "SRU_Gromp" => "NunuQBuffLizard",
            // Spooky Mystery Meat (MS on kill): Crimson Raptor/Raptor, Greater Murk Wolf/Murk
            // Wolf, Rift Scuttler. All 4.20 SR camps here are animals → Wolf ("Undead" Wraith
            // variant has no SR camp, so it only fires for the legacy models below).
            "SRU_Razorbeak" or "SRU_RazorbeakMini"
                or "SRU_Murkwolf" or "SRU_MurkwolfMini"
                or "Sru_Crab" => "NunuQBuffWolf",
            // legacy (Map1) models
            "AncientGolem" or "Golem" or "SmallGolem" => "NunuQBuffGolem",
            "LizardElder" or "YoungLizard" or "Dragon" or "Worm" => "NunuQBuffLizard",
            "Wraith" or "LesserWraith" or "GreatWraith" => "NunuQBuffWraith",
            "Wolf" or "GiantWolf" => "NunuQBuffWolf",
            _ => ""
        };
        if (!string.IsNullOrEmpty(buffName))
        {
            AddBuff(buffName, spell.SpellData.EffectLevelAmount[3][spell.CastInfo.SpellLevel], 1, spell, _nunu, _nunu);
        }
    }
}