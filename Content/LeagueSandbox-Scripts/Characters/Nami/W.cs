using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class NamiW : ISpellScript {
    private  ObjAIBase               _nami;
    private  AttackableUnit          _target;
    internal HashSet<AttackableUnit> HitUnits = new HashSet<AttackableUnit>();

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _nami, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    public void OnSpellPostCast(Spell spell) {
        HitUnits.Clear();
        if (_target == _nami) {
            var ap   = _nami.Stats.AbilityPower.Total * 0.6f;
            var heal = 65f + 30f * (_nami.GetSpell("NamiW").CastInfo.SpellLevel - 1) + ap;

            _target.TakeHeal(_nami, heal, HealType.SelfHeal);
            HitUnits.Add(_target);
            AddParticleTarget(_nami, _target, "Nami_Base_W_heal", _target);
            AddBuff("NamiPassive",         1.5f, 1, spell, _target, _nami);
            if (HitUnits.Count >= 3) return;
            var nextTarget = GetUnitsInRange(_nami, _target.Position, 800f, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)
                             .Where(x => !HitUnits.Contains(x))
                             .OrderBy(x => Vector2.Distance(_target.Position, x.Position))
                             .FirstOrDefault();
            if (nextTarget != null) {
                SpellCast(_nami, nextTarget.Team == _nami.Team ? 4 : 3, SpellSlotType.ExtraSlots, true, nextTarget,
                          _target.Position);
            }
        } else if (_target.Team == _nami.Team) {
            SpellCast(_nami, 2, SpellSlotType.ExtraSlots, true, _target, _nami.Position);
        } else if (_target.Team != _nami.Team) {
            SpellCast(_nami, 6, SpellSlotType.ExtraSlots, true, _target, _nami.Position);
        }
    }

    public void OnUpdateStats(AttackableUnit unit, float diff) {
        var bounceModifier = (-0.15f + 0.075f * (_nami.Stats.AbilityPower.Total / 100f)) * 100;
        SetSpellToolTipVar(_nami, 0, bounceModifier, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }
}

public class NamiWAlly : ISpellScript {
    private ObjAIBase               _nami;
    private HashSet<AttackableUnit> _hitUnits;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        var mainSpell = owner.GetSpell("NamiW");
        _hitUnits = (mainSpell.Script as NamiW).HitUnits;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        LogInfo("MissileAlly");
    }

    public void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ap   = _nami.Stats.AbilityPower.Total * 0.6f;
        var heal = 65f + 30f * (_nami.GetSpell("NamiW").CastInfo.SpellLevel - 1) + ap;

        target.TakeHeal(_nami, heal, HealType.SelfHeal);
        _hitUnits.Add(target);
        AddParticleTarget(_nami, target, "Nami_Base_W_heal", target);
        AddBuff("NamiPassive",         1.5f, 1, spell, target, _nami);
        if (_hitUnits.Count >= 3) return;
        var nextTarget = GetUnitsInRange(_nami, target.Position, 800f, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)
                         .Where(x => !_hitUnits.Contains(x))
                         .OrderBy(x => Vector2.Distance(target.Position, x.Position))
                         .FirstOrDefault();
        if (nextTarget != null) {
            SpellCast(_nami, nextTarget.Team == _nami.Team ? 4 : 3, SpellSlotType.ExtraSlots, true, nextTarget,
                      target.Position);
        }
    }
}

public class NamiWEnemy : ISpellScript {
    private ObjAIBase               _nami;
    private HashSet<AttackableUnit> _hitUnits;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        var mainSpell = owner.GetSpell("NamiW");
        _hitUnits = (mainSpell.Script as NamiW).HitUnits;
    }

    public void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ap  = _nami.Stats.AbilityPower.Total * 0.5f;
        var dmg = 70f + 40f * (_nami.GetSpell("NamiW").CastInfo.SpellLevel - 1) + ap;

        target.TakeDamage(_nami, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                          DamageResultType.RESULT_NORMAL);
        _hitUnits.Add(target);
        AddParticleTarget(_nami, target, "Nami_Base_W_tar", target);
        if (_hitUnits.Count >= 3) return;
        var nextTarget = GetUnitsInRange(_nami, target.Position, 800f, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)
                         .Where(x => !_hitUnits.Contains(x))
                         .OrderBy(x => Vector2.Distance(target.Position, x.Position))
                         .FirstOrDefault();
        if (nextTarget != null) {
            SpellCast(_nami, nextTarget.Team == _nami.Team ? 4 : 3, SpellSlotType.ExtraSlots, true, nextTarget,
                      target.Position);
        }
    }
}

public class NamiWMissileAlly : ISpellScript {
    private ObjAIBase               _nami;
    private HashSet<AttackableUnit> _hitUnits;


    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        var mainSpell = owner.GetSpell("NamiW");
        _hitUnits = (mainSpell.Script as NamiW).HitUnits;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }

    public void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ap             = _nami.Stats.AbilityPower.Total * 0.6f;
        var heal           = 65f + 30f * (_nami.GetSpell("NamiW").CastInfo.SpellLevel - 1) + ap;
        var bounceModifier = 1f  + (-0.15f + 0.075f * (_nami.Stats.AbilityPower.Total / 100f)) * _hitUnits.Count;
        switch (_hitUnits.Count) {
            case 0:  break;
            default: heal *= bounceModifier; break;
        }

        target.TakeHeal(_nami, heal, HealType.SelfHeal);
        _hitUnits.Add(target);
        AddBuff("NamiPassive",         1.5f, 1, spell, target, _nami);
        AddParticleTarget(_nami, target, "Nami_Base_W_tar_ally", target);
        if (_hitUnits.Count >= 3) return;
        var nextTarget = GetUnitsInRange(_nami, target.Position, 800f, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)
                         .Where(x => !_hitUnits.Contains(x))
                         .OrderBy(x => Vector2.Distance(target.Position, x.Position))
                         .FirstOrDefault();
        if (nextTarget != null) {
            SpellCast(_nami, nextTarget.Team == _nami.Team ? 4 : 3, SpellSlotType.ExtraSlots, true, nextTarget,
                      target.Position);
        }
    }
}

public class NamiWMissileEnemy : ISpellScript {
    private ObjAIBase               _nami;
    private HashSet<AttackableUnit> _hitUnits;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        var mainSpell = owner.GetSpell("NamiW");
        _hitUnits = (mainSpell.Script as NamiW).HitUnits;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }

    public void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ap             = _nami.Stats.AbilityPower.Total * 0.5f;
        var dmg            = 70f + 40f * (_nami.GetSpell("NamiW").CastInfo.SpellLevel - 1) + ap;
        var bounceModifier = 1f  + (-0.15f + 0.075f * (_nami.Stats.AbilityPower.Total / 100f)) * _hitUnits.Count;
        switch (_hitUnits.Count) {
            case 0:  break;
            default: dmg *= bounceModifier; break;
        }

        target.TakeDamage(_nami, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                          DamageResultType.RESULT_NORMAL);
        _hitUnits.Add(target);
        AddParticleTarget(_nami, target, "Nami_Base_W_tar_enemy", target);
        if (_hitUnits.Count >= 3) return;
        var nextTarget = GetUnitsInRange(_nami, target.Position, 800f, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)
                         .Where(x => !_hitUnits.Contains(x))
                         .OrderBy(x => Vector2.Distance(target.Position, x.Position))
                         .FirstOrDefault();
        if (nextTarget != null) {
            SpellCast(_nami, nextTarget.Team == _nami.Team ? 4 : 3, SpellSlotType.ExtraSlots, true, nextTarget,
                      target.Position);
        }
    }
}