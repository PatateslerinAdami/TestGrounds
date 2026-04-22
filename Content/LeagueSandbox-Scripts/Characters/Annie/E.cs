using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class MoltenShield : ISpellScript {
    private ObjAIBase _annie;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _annie = owner; }

    public void OnSpellPostCast(Spell spell) {
        AddBuff("Pyromania", 250000f, 1, spell, _annie, _annie, true);
        if (_annie.GetBuffsWithName("Pyromania").Count == 4) {
            RemoveBuff(_annie, "Pyromania");
            AddBuff("Pyromania_particle", 25000f, 1, spell, _annie, _annie, true);
        }

        AddBuff("MoltenShield", 5.0f, 1, spell, spell.CastInfo.Owner, spell.CastInfo.Owner);
    }
}