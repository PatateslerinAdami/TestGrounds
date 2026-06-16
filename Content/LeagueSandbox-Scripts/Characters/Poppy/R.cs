using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class PoppyDiplomaticImmunity : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
        CastingBreaksStealth = true,
    };

    AttackableUnit Target;

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        Target = target;
    }

    public void OnSpellCast(Spell spell)
    {
        var owner = spell.CastInfo.Owner;
        float[] durations = { 6f, 7f, 8f };
        float duration = durations[spell.CastInfo.SpellLevel - 1];

        AddBuff("PoppyDiplomaticImmunityDmg", duration, 1, spell, owner, owner);
        if (Target != null && !Target.IsDead)
        {
            AddBuff("PoppyDITarget", duration, 1, spell, Target, owner);
        }
    }
}
