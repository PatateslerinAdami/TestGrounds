using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

/// <summary>
/// Soraka R — Wish Cast Time VFX (ExtraSlot 3).
/// Client-rendered cast-time particle + sound.
/// Called by SorakaR.OnSpellPostCast via SpellCast(ExtraSlot 3).
/// </summary>
public class SorakaRCastTime : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        NotSingleTargetSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }

    public void OnSpellCast(Spell spell)
    {
        var owner = spell.CastInfo.Owner;

        // Cast-time VFX on Soraka (beam raising from staff)
        AddParticleTarget(owner, owner, "soraka_base_r_cas.troy", owner, lifetime: 1.5f);

        // Ground indicator (wish circle beneath Soraka)
        AddParticle(owner, null, "Soraka_Base_R_tar.troy", owner.Position, 1.5f);
    }

    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}
