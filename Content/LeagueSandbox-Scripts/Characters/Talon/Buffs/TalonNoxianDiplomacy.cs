using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class TalonNoxianDiplomacyBuff : IBuffGameScript {
    private ObjAIBase              _talon;
    private Spell          _spell;
    private Buff           _buff;
    private Particle       _p1;
    private Particle       _p2;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _talon = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _buff  = buff;
        _p1    = AddParticleTarget(_talon, _talon, "talon_Q_on_hit_ready_01", _talon, buff.Duration, bone:"L_Hand");
        _p2    = AddParticleTarget(_talon, _talon, "talon_Q_on_hit_ready_01", _talon, buff.Duration, bone:"R_Hand");
        _talon.SetAutoAttackSpell("TalonNoxianDiplomacyAttack", true);
        ApiEventManager.OnHitUnit.AddListener(this, _talon, OnHit);
    }

    private void OnHit(DamageData data) {
        var bonusDmg =
            30f + 30f * (_spell.CastInfo.SpellLevel - 1) +
            _talon.Stats.AttackDamage.FlatBonus;
        
        data.Damage += bonusDmg;
        data.PostMitigationDamage = data.Target.Stats.GetPostMitigationDamage(
            data.Damage,
            DamageType.DAMAGE_TYPE_PHYSICAL,
            _talon
        );

        if (IsValidTarget(_talon, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)) {
            AddBuff("TalonBleedBuff", 6f, 1, _spell, data.Target, _talon);
        }
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        _talon.ResetAutoAttackSpell();
    }
}