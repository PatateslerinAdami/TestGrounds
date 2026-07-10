using System.Threading;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class PantheonPassiveShield : IBuffGameScript
{
    private Particle _p;
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsNonDispellable = true,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.OnPreTakeDamage.AddListener(this, unit, OnPreTakeDamage);
        _p = AddParticleTarget(unit, unit, "Pantheon_Base_P_buf", unit, bone: "C_BUFFBONE_GLB_CENTER_LOC", targetBone: "C_Buffbone_Glb_Center_Loc",
            lifetime: -1f, size: 1, flags: FXFlags.SimulateWhileOffScreen);
    }

    private void OnPreTakeDamage(DamageData data)
    {
        if(IsValidTarget(data.Target, data.Attacker,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectBuildings | SpellDataFlags.AffectMinions |SpellDataFlags.IgnoreLaneMinion)) return;
        if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
        data.Damage = 0f;
        data.PostMitigationDamage = 0f;
        data.DamageResultType = DamageResultType.RESULT_INVULNERABLENOMESSAGE;
        RemoveBuff(data.Target, "PantheonPassiveShield");
        Say(data.Target, "game_lua_Aegis_Block");
    }


    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_p);
    }
}