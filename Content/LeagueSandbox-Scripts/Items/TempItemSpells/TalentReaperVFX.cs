using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TalentReaperVFX : ISpellScript
{
    private const float SPOILS_HEAL_AMOUNT_FLAT       = 40f;
    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        }
    };

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        if (target == null) return;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit, true);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        if (target == null) return;

        var owner = spell.CastInfo.Owner;
        target.TakeHeal(owner, SPOILS_HEAL_AMOUNT_FLAT, HealType.SelfHeal);

        missile?.SetToRemove();
    }
}