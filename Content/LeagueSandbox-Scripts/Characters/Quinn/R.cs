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
/// Quinn R — unified form change + Skystrike.
/// Uses a buff ("QuinnValorForm") to track Valor state across SetSpell resets.
/// </summary>
public class QuinnR : ISpellScript
{
    private ObjAIBase _owner;
    private bool      _isValor;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell)
    {
        AddParticleTarget(_owner, _owner, "Quinn_Base_R_Cas.troy", _owner, 2f);
    }

    public void OnSpellPostCast(Spell spell)
    {
        if (_isValor)
        {
            DoSkystrike(spell);
        }
        else
        {
            TransformToValor(spell);
        }
    }

    private void TransformToValor(Spell spell)
    {
        _isValor = true;
        _owner.ChangeModel("QuinnValor");
        _owner.SetSpell("QuinnValorQ", 0, true);
        _owner.SetSpell("QuinnW", 1, true);
        _owner.SetSpell("QuinnValorE", 2, true);

        _owner.Stats.Range.BaseValue = 125f;
        int rank = spell.CastInfo.SpellLevel;
        _owner.Stats.MoveSpeed.PercentBonus = rank switch { 1 => 0.20f, 2 => 0.30f, 3 => 0.40f, _ => 0.20f };

        AddParticleTarget(_owner, _owner, "Quinn_Base_R_Transform.troy", _owner, 2f);
    }

    private void DoSkystrike(Spell spell)
    {
        int rank = spell.CastInfo.SpellLevel;
        float dmg = rank switch { 1 => 100, 2 => 150, 3 => 200, _ => 100 };
        float totalAD = _owner.Stats.AttackDamage.Total * 1.0f;
        float final = dmg + totalAD;

        AddParticle(_owner, null, "Quinn_Base_R_Skystrike.troy", _owner.Position, 3f);

        var enemies = GetUnitsInRange(_owner, _owner.Position, 400f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var u in enemies)
        {
            if (u.Team == _owner.Team) continue;
            u.TakeDamage(_owner, final, DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            AddParticleTarget(_owner, u, "Quinn_Base_R_Tar.troy", u, 1f);
        }

        // Revert
        _isValor = false;
        _owner.ChangeModel("Quinn");
        _owner.SetSpell("QuinnQ", 0, true);
        _owner.SetSpell("QuinnW", 1, true);
        _owner.SetSpell("QuinnE", 2, true);
        _owner.Stats.Range.BaseValue = 525f;
        _owner.Stats.MoveSpeed.PercentBonus = 0f;
        AddParticleTarget(_owner, _owner, "Quinn_Base_R_Revert.troy", _owner, 1.5f);
    }

    public void OnUpdate(float diff) { }
}
