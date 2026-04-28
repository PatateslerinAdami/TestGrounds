using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells;

public class ElixirOfIron : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        // TODO
    };

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        if (owner.HasBuff("ElixirOfRuin")) {
            RemoveBuff(owner, "ElixirOfRuin");
        }
        if (owner.HasBuff("ElixirOfIron")) {
            RemoveBuff(owner, "ElixirOfIron");
        }
        if (owner.HasBuff("ElixirOfSorcery")) {
            RemoveBuff(owner, "ElixirOfSorcery");
        }
        if (owner.HasBuff("ElixirOfWrath")) {
            RemoveBuff(owner, "ElixirOfWrath");
        }
        AddBuff("ElixirOfIron", 180f, 1, spell, owner, owner);
    }
}