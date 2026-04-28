using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptMorgana : ICharScript {
    private ObjAIBase _morgana;

    private StatsModifier StatsModifier { get; } = new();
    
    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _morgana = owner;
        ApiEventManager.OnLevelUp.AddListener(this, _morgana, OnLevelUp);
        StatsModifier.SpellVamp.FlatBonus = 0.1f;
        _morgana.AddStatModifier(StatsModifier);
    }

    private void OnLevelUp(AttackableUnit target) {
        var level = _morgana.Stats.Level;
        _morgana.RemoveStatModifier(StatsModifier);
        StatsModifier.SpellVamp.FlatBonus = level switch {
            < 7  => 0.1f,
            < 13 => 0.15f,
            _    => 0.2f
        };
        _morgana.AddStatModifier(StatsModifier);
    }
}