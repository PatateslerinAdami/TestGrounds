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

// Replay-verified Q wire flow per cast (id=10953, t=260303):
//   +0ms    CastSpellAns        slot=0  designerCast=0.25  total=1.0  targets=[click_target]
//   +225ms  ForceCreateMissile  (at end of cast windup)
//   Per hit (max 5 bounces, all client-side simulated):
//     +X    BuffAdd2  KatarinaQMarkSpellShieldCheck (hash=28814379)  dur=0.25
//             ↳ throwaway marker that consumes the target's spell shield (Banshee/Sivir E)
//               during a 250ms trigger window, so the real QMark is shield-bypass.
//     +X    FX_Create_Group  [katarina_daggered.troy + katarina_bouncingBlades_tar.troy,
//                              both bind=enemy, no bone]
//     +Y    BuffAdd2  KatarinaQMark (hash=84848667)  dur=4.25
//   No per-bounce ForceCreateMissile, no S2C_ChainMissileSync. Bouncing handled in client.
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
            case 7: AddParticleTarget(_katarina, target, "katarina_XMas_bouncingBlades_tar", target); break;
            case 6: AddParticleTarget(_katarina, target, "katarina_bouncingBlades_tar_sand", target); break;
            default: AddParticleTarget(_katarina, target, "katarina_bouncingBlades_tar", target); break;
        }
        target.TakeDamage(_katarina, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
            false);

        // Spell-shield bypass marker — replay-verified buff name. Consumed by spell-shields
        // (Banshee/Sivir E/Yasuo W) within its 0.25s window, after which the real QMark is
        // applied. Our codebase has no shield-consume logic yet (see project_spell_shield_
        // system_deferred memory), so this is currently wire-fidelity only.
        AddBuff("KatarinaQMarkSpellShieldCheck", 0.25f, 1, spell, target, _katarina);
        AddBuff("KatarinaQMark", 4.0f, 1, spell, target, _katarina);
    }

    private void OnStatsUpdate(AttackableUnit unit, float diff)
    {
        var bonusAp = _katarina.Stats.AbilityPower.Total * 0.45f;
        SetSpellToolTipVar(_katarina, 0, bonusAp, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
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
