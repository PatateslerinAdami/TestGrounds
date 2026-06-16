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

/// <summary>
/// Soraka R — Wish (4.20).
/// Global heal all allied champions.
/// +50% healing for allies below 40% HP.
/// Casts ExtraSlot 3 (SorakaRCastTime) so client renders cast-time VFX.
/// </summary>
public class SorakaR : ISpellScript
{
    private ObjAIBase _soraka;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _soraka = owner;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }

    public void OnSpellCast(Spell spell)
    {
        // Caster VFX — beam raising from staff
        AddParticleTarget(_soraka, _soraka, "soraka_base_r_cas.troy", _soraka, 2f);
    }

    public void OnSpellPostCast(Spell spell)
    {
        // ExtraSlot 3 = SorakaRCastTime — client renders AfterEffect + HitEffect
        SpellCast(_soraka, 3, SpellSlotType.ExtraSlots, true, _soraka, _soraka.Position);

        var owner = spell.CastInfo.Owner;
        int rank = spell.CastInfo.SpellLevel;
        float ap = owner.Stats.AbilityPower.Total * spell.SpellData.Coefficient;

        float baseHeal = rank switch
        {
            1 => 150f, 2 => 250f, 3 => 350f, _ => 150f
        } + ap;

        var allies = GetChampionsInRange(owner, owner.Position, 99999f, false,
            getAllies: true, getEnemies: false);

        foreach (var unit in allies)
        {
            if (unit.IsDead) continue;

            float heal = baseHeal;
            float hpPercent = unit.Stats.CurrentHealth / unit.Stats.HealthPoints.Total;
            if (hpPercent < 0.40f)
            {
                heal *= 1.5f;
            }

            unit.TakeHeal(owner, heal, HealType.SelfHeal);

            // Heal VFX on each ally
            AddParticleTarget(owner, unit, "Soraka_Base_R_tar.troy", unit, 2f);
            AddParticleTarget(owner, unit, "Global_Heal.troy", unit, 1.5f);
        }
    }

    public void OnUpdate(float diff) { }
}
