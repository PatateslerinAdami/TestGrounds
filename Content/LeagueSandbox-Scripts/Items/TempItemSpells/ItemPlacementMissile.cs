using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class ItemPlacementMissile : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        /*NotSingleTargetSpell = false,
        DoesntBreakShields = false,
        TriggersSpellCasts = true,
        CastingBreaksStealth = false,
        IsNonDispellable = true,*/
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Arc,
            OverrideHeightAugment = 100f
        }
    };

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
    }
}