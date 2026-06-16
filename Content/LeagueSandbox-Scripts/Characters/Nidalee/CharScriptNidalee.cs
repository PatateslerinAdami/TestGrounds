using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace CharScripts;

/// <summary>
/// Nidalee passive — Prowl.
/// +15% MS in brush for 2s after leaving brush.
/// +30% MS toward Hunted enemy champions within 5500 range.
/// Full implementation deferred — minimal stub for now.
/// </summary>
public class CharScriptNidalee : ICharScript
{
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null) { }
    public void OnPostActivate(ObjAIBase owner, Spell spell = null) { }
    public void OnUpdate(float diff) { }
}
