using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AatroxR : ISpellScript {
    private ObjAIBase _aatrox;
    private string    _pcastname;
    private string    _phitname;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = false
        // TODO
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
        switch (_aatrox.SkinID) {
            case 0:
                _pcastname = "Aatrox_Base_R_Activate";
                _phitname  = "Aatrox_Base_R_active_hit_tar";
                break;
            case 1:
                _pcastname = "Aatrox_Skin01_R_Activate";
                _phitname  = "Aatrox_Skin01_R_active_hit_tar";
                break;
            case 2:
                _pcastname = "Aatrox_Skin02_R_Activate";
                _phitname  = "Aatrox_Skin02_R_active_hit_tar";
                break;
        }
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {

        AddParticleTarget(_aatrox, _aatrox, _pcastname, _aatrox);

        float apRatio = _aatrox.Stats.AbilityPower.Total;
        float damage  = 200 + (100 * spell.CastInfo.SpellLevel - 1) + apRatio;
        //AddParticle(_aatrox, _aatrox, "Aatrox_Base_R_Aura_Self", _aatrox.Position);
        

        var enemiesInRange = GetUnitsInRange(_aatrox, _aatrox.Position, 550, true,
                                             SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                             SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var enemy in enemiesInRange) {
            AddParticleTarget(_aatrox, enemy, _phitname, enemy);
            enemy.TakeDamage(_aatrox, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                            false);
            SpellCast(_aatrox, 5, SpellSlotType.ExtraSlots, true, _aatrox, enemy.Position);
        }
        AddBuff("AatroxR", 12f, 1, spell, spell.CastInfo.Owner, spell.CastInfo.Owner);
    }
}

public class AatroxRHeal : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        MissileParameters = new MissileParameters() {
          Type  = MissileType.Target,
        },
    };
}