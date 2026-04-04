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

internal class JudicatorHolyFervor : IBuffGameScript {
    private ObjAIBase _kayle;
    private Spell     _spell;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _kayle = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        ApiEventManager.OnHitUnit.AddListener(this, _kayle, OnHit);
    }

    private void OnHit(DamageData data) {
        if (!IsValidTarget(_kayle, data.Target,
                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                           SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        AddBuff("JudicatorHolyFervorDebuff", 5f, 1, _spell, data.Target, _kayle);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}