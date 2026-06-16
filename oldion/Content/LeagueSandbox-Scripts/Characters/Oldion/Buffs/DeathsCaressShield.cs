using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class DeathsCaressShield : IBuffGameScript
{
    private ObjAIBase _oldion;
    private Buff _buff;
    private float _shieldHp;
    public StatsModifier StatsModifier { get; } = new();

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.SPELL_SHIELD,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
    };

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _oldion = ownerSpell.CastInfo.Owner;
        _buff = buff;
        int level = ownerSpell.CastInfo.SpellLevel;
        float[] shieldVals = { 0, 100f, 150f, 200f, 250f, 300f };
        _shieldHp = shieldVals[Math.Clamp(level, 0, 5)] + _oldion.Stats.AbilityPower.Total * 0.9f;
        ApiEventManager.OnTakeDamage.AddListener(this, _oldion, OnTakeDamage, false);
    }

    private void OnTakeDamage(DamageData data)
    {
        if (_shieldHp <= 0) return;
        if (_shieldHp >= data.Damage)
        {
            _shieldHp -= data.Damage;
            data.Damage = 0;
        }
        else
        {
            data.Damage -= _shieldHp;
            _shieldHp = 0;
            _buff.DeactivateBuff();
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        int level = ownerSpell.CastInfo.SpellLevel;
        float[] baseDmg = { 0, 100f, 150f, 200f, 250f, 300f };
        float ap = _oldion.Stats.AbilityPower.Total * 0.9f;
        float dmg = baseDmg[Math.Clamp(level, 0, 5)] + ap;

        var units = GetUnitsInRange(_oldion, _oldion.Position, 525f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectNeutral);

        foreach (var target in units)
        {
            if (target.Team != _oldion.Team && target is not ObjBuilding)
                target.TakeDamage(_oldion, dmg, DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
        }

        if (_oldion.HasBuff("DeathsCaress"))
            _oldion.SetSpell("OldionDeathsCaress", 1, true);
    }
}
