using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class KatarinaQ : ISpellScript
{
    Spell _qMis;
    ObjAIBase _katarina;
    private readonly Dictionary<uint, HashSet<AttackableUnit>> _chainHitUnits = new();

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Chained,
            BounceSpellName = "KatarinaQMis",
            CanHitSameTarget = false,
            CanHitSameTargetConsecutively = false,
            MaximumHits = 5
        },
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        CastTime = 0.25f,
        AutoCooldownByLevel = [10, 9.5f, 9f, 8.5f, 8f]
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _qMis = spell;
        _katarina = owner;
        ApiEventManager.OnSpellHit.AddListener(this, _qMis, TargetExecute, false);
        ApiEventManager.OnUpdateStats.AddListener(this, _katarina, OnStatsUpdate);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        var ap = _katarina.Stats.AbilityPower.Total * 0.45f;
        var dmg = 60 + 25 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1 - 1) + ap;
        switch (_katarina.SkinID)
        {
            case 9: AddParticleTarget(_katarina, target, "Katarina_Skin09_Q_tar", target); break;
            case 7:
                AddParticleTarget(_katarina, target, "katarina_XMas_bouncingBlades_tar", target);
                ;
                break;
            case 6:
                AddParticleTarget(_katarina, target, "katarina_bouncingBlades_tar_sand", target);
                ;
                break;
            default: AddParticleTarget(_katarina, target, "katarina_bouncingBlades_tar", target); break;
        }
        target.TakeDamage(_katarina, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
            false);

        //ConsumeDaggered(target);
        AddBuff("KatarinaQMark", 4.0f, 1, spell, target, _katarina);

        /*switch (_katarina.SkinId) {
            case 9: AddParticleTarget(_katarina, target, "katarina_Skin09_Q_Cast", target); break;
            default: AddParticleTarget(_katarina, target, "katarina_bouncingBlades_cas", target); break;
        }*/
    }

    private void OnStatsUpdate(AttackableUnit unit, float diff)
    {
        var bonusAp = _katarina.Stats.AbilityPower.Total * 0.45f;
        SetSpellToolTipVar(_katarina, 0, bonusAp, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }

    private void ConsumeDaggered(AttackableUnit target)
    {
        if (!target.HasBuff("KatarinaQMark")) return;

        var markApRatio = _katarina.Stats.AbilityPower.Total * 0.2f;
        var markDamage = 15 + 15 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1) + markApRatio;

        target.TakeDamage(_katarina, markDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
            false);
        RemoveBuff(target, "KatarinaQMark");
    }
}

public class KatarinaQMis : ISpellScript
{

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
    {
        MissileParameters = new MissileParameters
        {
        },
        TriggersSpellCasts = false,
        IsDamagingSpell = true
    };
}