using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class EmpathizeAura : IBuffGameScript
{
    private ObjAIBase _morgana;
    private Buff _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        IsHidden = true,
        PersistsThroughDeath = true,
        IsNonDispellable = true
    };

    public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _morgana = buff.SourceUnit;
        _buff = buff;
        ApiEventManager.OnLevelUp.AddListener(this, _morgana, OnLevelUp);
        ApiEventManager.OnUpdateStats.AddListener(this, _morgana, OnUpdateStats);
        StatsModifier.SpellVamp.FlatBonus = 0.1f;
        _morgana.AddStatModifier(StatsModifier);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff)
    {
        var level = _morgana.Stats.Level;
        var amount = level switch
        {
            < 7 => 10f,
            < 13 => 15f,
            _ => 20f
        };
        SetBuffToolTipVar(_buff, 0, amount);
    }

    private void OnLevelUp(AttackableUnit target)
    {
        var level = _morgana.Stats.Level;
        _morgana.RemoveStatModifier(StatsModifier);
        StatsModifier.SpellVamp.FlatBonus = level switch
        {
            < 7 => 0.1f,
            < 13 => 0.15f,
            _ => 0.2f
        };
        _morgana.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
    }
}