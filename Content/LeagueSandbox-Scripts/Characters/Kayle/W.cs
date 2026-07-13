using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class JudicatorDivineBlessing : ISpellScript {
    private ObjAIBase      _kayle;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = false,
        TriggersSpellCasts   = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _kayle = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    public void OnSpellPostCast(Spell spell) {
        PlayAnimation(_kayle, "Spell2", 0,0, 1, AnimationFlags.NoBlend | AnimationFlags.Junk6 | AnimationFlags.Junk7);
        AddBuff("JudicatorDivineBlessing", 3f, 1, spell, _target, _kayle);
    }
}