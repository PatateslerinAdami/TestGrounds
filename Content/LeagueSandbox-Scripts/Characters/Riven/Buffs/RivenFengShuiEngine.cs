using System;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class RivenFengShuiEngine : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

    private Particle _swordFx;
    private Particle _handFxL;
    private Particle _handFxR;
    private Spell _ownerSpell;
    private ObjAIBase _ownerUnit;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        if (unit is not Champion owner) return;
        _ownerUnit = owner;
        _ownerSpell = ownerSpell;

        var spellLevel = ownerSpell.CastInfo.SpellLevel;
        float adPercent = 0.15f + 0.05f * spellLevel;

        StatsModifier.AttackDamage.PercentBonus = adPercent;
        StatsModifier.Range.FlatBonus = 75f;
        unit.AddStatModifier(StatsModifier);

        _swordFx = AddParticleTarget(owner, owner, "exile_ult_blade_swap_base.troy", owner,
            buff.Duration, bone: "BUFFBONE_GLB_WEAPON_2");
        _handFxL = AddParticleTarget(owner, owner, "exile_ult_attack_buf.troy", owner,
            buff.Duration, bone: "L_HAND");
        _handFxR = AddParticleTarget(owner, owner, "exile_ult_attack_buf.troy", owner,
            buff.Duration, bone: "R_HAND");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_swordFx);
        RemoveParticle(_handFxL);
        RemoveParticle(_handFxR);

        if (_ownerUnit == null || _ownerSpell == null) return;

        if (_ownerUnit.Spells[3].SpellName == "RivenIzunaBlade")
        {
            var spellLevel = _ownerSpell.CastInfo.SpellLevel;
            var fengSpell = _ownerUnit.SetSpell("RivenFengShuiEngine", 3, true);

            float[] cooldowns = { 110f, 85f, 50f };
            int idx = Math.Min(spellLevel - 1, 2);
            fengSpell.SetCooldown(cooldowns[idx], false);
        }
    }

    public void OnUpdate(float diff) { }
}
