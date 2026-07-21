using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class SivirPassive : IBuffGameScript {
    private ObjAIBase _sivir;
    private Spell     _spell;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _sivir = buff.SourceUnit;
        _spell = ownerSpell;
        ApiEventManager.OnHitUnit.AddListener(this, _sivir, OnHit);
        ApiEventManager.OnSpellHit.AddListener(this, _sivir.Spells[0], OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        if (!IsValidTarget(_sivir, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)) return;
        
        AddBuff("SivirPassiveSpeed", 2f, 1, _spell, _sivir, _sivir);
    }

    private void OnHit(DamageData data)
    {
        if (!IsValidTarget(_sivir, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)) return;
        AddBuff("SivirPassiveSpeed", 2f, 1, _spell, _sivir, _sivir);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}