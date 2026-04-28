using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells;

public class ElixirOfSorcery : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {};

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
        AddBuff("ElixirOfSorcery", 180.0f, 1, spell, owner, owner);
    }
}