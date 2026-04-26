using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AkaliShadowSwipe : ISpellScript {
    private ObjAIBase _akali;
    private Spell _spell;
    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true,
        NotSingleTargetSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
         _akali = owner;
         _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnStatsUpdate);
    }

    public void OnSpellPostCast(Spell spell) {
        var unitsInRange = GetUnitsInRange(_akali, _akali.Position, 300f, true,
                        SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                        SpellDataFlags.AffectNeutral);

        foreach (var unit in unitsInRange) {
            SlashTarget(unit);
        }
    }

    private void SlashTarget(AttackableUnit target) {
        var ap          = _spell.CastInfo.Owner.Stats.AbilityPower.Total * 0.4f;
        var ad          = _spell.CastInfo.Owner.Stats.AttackDamage.Total * 0.6f;
        var damage      = 30 + 25 * (_spell.CastInfo.SpellLevel - 1) + ap + ad;
        AddParticle(_akali, _akali, "akali_shadowSwipe_cas", _akali.Position);
        AddParticleTarget(_akali, target, "akali_shadowSwipe_tar", target);
        target.TakeDamage(_akali, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
        ApiEventManager.OnSpellHit.Publish(_spell, (target, null, null));
    }
    
    private void OnStatsUpdate(AttackableUnit unit, float diff) {
        var bonusAd = _akali.Stats.AttackDamage.Total * 0.6f;
        SetSpellToolTipVar(_akali, 0, bonusAd, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }
}
