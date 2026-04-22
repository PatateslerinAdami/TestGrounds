using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class EvelynnW : ISpellScript {
    private ObjAIBase _evelynn;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _evelynn        = owner;
        _spell         = spell;
        ApiEventManager.OnAssist.AddListener(this, _evelynn, OnAssist);
        ApiEventManager.OnKill.AddListener(this, _evelynn, OnKill);
    }

    private void OnKill(DeathData data) {
        _spell.SetCooldown(0f, true);
    }
    
    private void OnAssist(ObjAIBase obj, DeathData data) {
        _spell.SetCooldown(0f, true);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var buffs = _evelynn.GetBuffs();
        foreach (var buff in buffs.Where(buff => buff.BuffType == BuffType.SLOW)) { RemoveBuff(buff); }
        AddParticleTarget(_evelynn, _evelynn, "Evelynn_W_cas", _evelynn);
        AddBuff("EvelynnW", 3f, 1, spell, _evelynn, _evelynn);
    }

    public void OnSpellPostCast(Spell spell) {
    }
}