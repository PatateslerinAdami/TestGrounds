using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

// ============================================================================
// QuinnW — Heightened Senses: Vision reveal + passive AS steroid
// Same spell in both forms (QuinnW = QuinnW in Valor too)
// ============================================================================
public class QuinnW : ISpellScript
{
    private ObjAIBase _owner;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell)
    {
        // Vision reveal around Quinn (2100 range)
        AddPosPerceptionBubble(_owner.Position, 2100f, 2f, _owner.Team);
        AddParticle(_owner, null, "Quinn_Base_W_Reveal.troy", _owner.Position, 2f);

        // Passive: AS bonus when hitting Vulnerable targets — deferred to CharScript
    }
    public void OnUpdate(float diff) { }
}

// Valor form shares QuinnW — no separate class needed
