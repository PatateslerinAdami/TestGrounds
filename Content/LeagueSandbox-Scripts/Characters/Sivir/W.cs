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

public class SivirW : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = false,
    };


    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        AddBuff("SivirW", 6f, 1, spell, owner, owner);
    }
}

public class SivirWAttack : ISpellScript
{
    private ObjAIBase _sivir;

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Chained,
            BounceSpellNameEnemy = "SivirWAttackBounce",
            CanHitSameTarget = false,
            BounceSelection = BounceSelection.Nearest,
            MaximumHits = 32,
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _sivir = owner;
        // MUST use the `spell` parameter: OnActivate runs inside this spell's own
        // constructor, BEFORE it is registered in the spellbook — GetSpell("SivirWAttack")
        // returns null here and AddListener silently swallows null sources.
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnWAttackHit);
    }

    private void OnWAttackHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        if (missile == null || missile.HitCount <= 1) {
            return; // first hit = engine AutoAttackHit (full AA, crit, on-hits)
        }
  
        // Bounce: Effect3% total AD. Crit was already rolled at segment spawn and baked
        // into the wire packet — READ it, don't roll (one source of truth).
        var w   = _sivir.GetSpell("SivirW");
        var pct = w.SpellData.EffectLevelAmount[3][w.CastInfo.SpellLevel] / 100f;
        var dmg = _sivir.Stats.AttackDamage.Total * pct;

        var isCrit = missile.CastInfo.Targets.Count > 0
                     && missile.CastInfo.Targets[0].HitResult == HitResult.HIT_Critical;
        if (isCrit) {
            dmg *= _sivir.Stats.CriticalDamage.Total;
        }
        target.TakeDamage(_sivir, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK,
            isCrit ? DamageResultType.RESULT_CRITICAL : DamageResultType.RESULT_NORMAL);
    }
}

public class SivirWAttackBounce : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
    };
}