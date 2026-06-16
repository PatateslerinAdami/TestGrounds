using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace CharScripts;

/// <summary>
/// Quinn passive — Harrier. Marks a random nearby enemy as Vulnerable every few seconds.
/// Attacks against Vulnerable targets deal bonus physical damage.
/// Deferred — minimal stub.
/// </summary>
public class CharScriptQuinn : ICharScript
{
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null) { }
    public void OnPostActivate(ObjAIBase owner, Spell spell = null) { }
    public void OnUpdate(float diff) { }
}
