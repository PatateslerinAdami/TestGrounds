using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class SionPassiveDelay : IBuffGameScript
{
    private ObjAIBase _sion;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        PersistsThroughDeath = true,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _sion = buff.SourceUnit;
        HideHealthBar(unit, hide: true);
        unit.SetStatus(StatusFlags.Targetable, false);
        unit.SetStatus(StatusFlags.Invulnerable, true);
        unit.SetStatus(StatusFlags.CanMove, false);
        unit.SetStatus(StatusFlags.CanAttack, false);


        SetCharacterVoiceOverride(buff.SourceUnit, "Max");
        //PlayAnimation(unit, "Passive_idle1", 0f, 0, 1, AnimationFlags.Lock | AnimationFlags.NoBlend | AnimationFlags.Junk5 |AnimationFlags.Junk7);
    }

    public void OnUpdate(Buff buff, float diff)
    {
        ExecutePeriodically(buff.BuffVars, "SionPassiveSound", 100f, false, 1,
            () =>
            {
                PlayAnimation(buff.SourceUnit, "Passive_Death", 1.7f, 0, 1, AnimationFlags.Lock | AnimationFlags.Junk7);
            });
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        unit.SetStatus(StatusFlags.Targetable, true);
        unit.SetStatus(StatusFlags.Invulnerable, false);
        unit.SetStatus(StatusFlags.CanMove, true);
        unit.SetStatus(StatusFlags.CanAttack, true);
        HideHealthBar(unit, hide: false);
        StopAnimation(unit, "Passive_Death", StopAnimationFlags.IgnoreLock);
        AddBuff("SionPassiveZombie", 60f, 1, ownerSpell, _sion, _sion);
        AddBuff("SionPassiveSoundEnd", 60f, 1, ownerSpell, unit, _sion);
    }
}