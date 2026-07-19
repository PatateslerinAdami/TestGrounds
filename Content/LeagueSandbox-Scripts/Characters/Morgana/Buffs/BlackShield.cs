using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
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

internal class BlackShield : IBuffGameScript
{
    private ObjAIBase _morgana;
    private Shield _blackShield;
    private Particle _blackShieldParticle;
    private Buff _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        //Internal handeling
        BuffType = BuffType.SPELL_IMMUNITY,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _morgana = buff.SourceUnit;
        _buff = buff;
        var particleName = _morgana.SkinID switch
        {
            4 => "Morgana_Blackthorn_Blackshield.troy",
            6 => "Morgana_Skin06_E_Tar.troy",
            _ => "Morgana_Base_E_Tar.troy"
        };
        _blackShieldParticle = SpellEffectCreate(particleName, _morgana, unit, unit, lifetime: buff.Duration,
            boneName: "C_Buffbone_Glb_Center_Loc");
        ApiEventManager.OnAllowAddBuff.AddListener(this, unit, OnAllowAddBuff);
        var shieldHealth = ownerSpell.SpellData.EffectLevelAmount[1][ownerSpell.CastInfo.SpellLevel] + _morgana.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
        _blackShield = new Shield(_morgana, _morgana, false, true, shieldHealth, buff);
        _morgana.AddShield(_blackShield);
        ApiEventManager.OnShieldBreak.AddListener(this, _blackShield, OnShieldBreak);
    }

    private bool OnAllowAddBuff(AttackableUnit target, AttackableUnit unit, Buff buff)
    {
        if (buff.BuffType is BuffType.BLIND or BuffType.CHARM or BuffType.DISARM or BuffType.FEAR or BuffType.FLEE
            or BuffType.FRENZY or BuffType.KNOCKBACK or BuffType.KNOCKUP or BuffType.NEAR_SIGHT or BuffType.POLYMORPH
            or BuffType.SLEEP or BuffType.SILENCE or BuffType.SLOW or BuffType.SNARE or BuffType.STUN or BuffType.TAUNT
            or BuffType.SUPPRESSION)
        {
            Say(unit, "game_lua_BlackShield_immune");
            return true;
        }
        else
        {
            return false;
        }
    }

    private void OnShieldBreak(Shield shield)
    {
        RemoveBuff(_buff);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _morgana.RemoveShield(_blackShield);
        RemoveParticle(_blackShieldParticle);
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}