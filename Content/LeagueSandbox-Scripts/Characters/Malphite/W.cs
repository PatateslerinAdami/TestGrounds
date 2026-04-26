using System.Linq;
using System.Numerics;
using Buffs;
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
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class Obduracy : ISpellScript {
    private ObjAIBase _malphite;
    private Spell     _obduracySpell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts   = true,
        NotSingleTargetSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _malphite = owner;
        _obduracySpell = spell;
        ApiEventManager.OnLevelUpSpell.AddListener(this, _obduracySpell, OnLevelUp);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        AddBuff("ObduracyBuff", 6f, 1, spell, _malphite, _malphite);
    }

    private void OnLevelUp(Spell spell) {
        if (spell.CastInfo.SpellLevel == 1) {
            AddBuff("MalphiteCleave", 25000, 1, spell,_malphite, _malphite, true);
        }
    }
}

public class ObduracyAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell = true
    };
}