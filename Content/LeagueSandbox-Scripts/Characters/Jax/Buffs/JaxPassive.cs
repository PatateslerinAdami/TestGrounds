using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class JaxPassive : IBuffGameScript
{
    private ObjAIBase _jax;
    private Spell _spell;
    private Buff _buff;
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {

        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = true,
        PersistsThroughDeath = true,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell)
    {
        _jax = buff.SourceUnit;
        _spell = ownerspell;
        _buff = buff;
        ApiEventManager.OnHitUnit.AddListener(this, _jax, OnHit);
        ApiEventManager.OnUpdateStats.AddListener(this, unit, OnUpdateStats);
    }

    private void OnHit(DamageData data)
    {
        AddBuff("JaxRelentlessAssaultAS", 2.5f, 1, _spell, _jax, _jax);
    }
    
    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var attackSpeed = _jax.Stats.Level switch {
            <4  => 4f,
            <7  => 6f,
            <10 => 8f,
            <13 => 10f,
            <16 => 12f,
            _   => 14
        };
        SetBuffToolTipVar(_buff, 0, attackSpeed);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
    {
    }
}