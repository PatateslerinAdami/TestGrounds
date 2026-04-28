using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class KarmaSolKimShield : ISpellScript {
    private ObjAIBase _karma;
    private ObjAIBase _target;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _karma = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _karma, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = owner;
        if (_karma.HasBuff("KarmaMantra")) {
            AddBuff("KarmaSolKimShieldLocket", 4f, 1, spell, target, _karma);

            var alliedUnits = GetUnitsInRange(_karma, target.Position, 700f, true,
                                              SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes).Where(unit => unit != target);
            foreach (var alliedUnit in alliedUnits) {
                AddBuff("KarmaSolKimShieldLocket", 4f, 1, spell, alliedUnit, _karma);
            }
            
            var enemyUnits = GetUnitsInRange(_karma, target.Position, 700f, true,
                                             SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                             SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
            foreach (var enemyUnit in enemyUnits) {
                var apDmg           = _karma.Stats.AbilityPower.Total * 0.6f;
                var dmg             = 60f  + 60f   * (spell.CastInfo.SpellLevel            - 1) + apDmg;
                var reductionAmount = 0.5f + 0.25f * (_karma.Spells[3].CastInfo.SpellLevel -1);
                _karma.Spells[3].LowerCooldown(reductionAmount);
                enemyUnit.TakeDamage(_karma, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                                     DamageResultType.RESULT_NORMAL);
                AddParticleTarget(_karma,enemyUnit,"Karma_Base_E_unit_tar_R_01",enemyUnit);
            }
        } else {
            AddBuff("KarmaSolKimShield", 4f, 1, spell, target, _karma);
        }
        
    }

    public void OnSpellPostCast(Spell spell) {
        
    }
    
    private void OnUpdateStats(AttackableUnit target, float diff) {
        var dmg = 60f + 60f * (_karma.GetSpell("KarmaSolKimShield").CastInfo.SpellLevel - 1);
        SetSpellToolTipVar(_karma, 2, dmg, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}