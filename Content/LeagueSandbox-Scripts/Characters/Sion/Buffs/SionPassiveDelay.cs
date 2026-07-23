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
using DeathData = LeaguePackets.Game.Common.DeathData;

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
        _sion.Stats.CurrentHealth = 1f;
        _sion.StopMovement();
        unit.SetStatus(StatusFlags.Targetable, false);
        unit.SetStatus(StatusFlags.Invulnerable, true);
        unit.SetStatus(StatusFlags.CanMove, false);
        unit.SetStatus(StatusFlags.CanAttack, false);
        unit.SetStatus(StatusFlags.CanCast, false);
        _sion.Spells[4].SetCooldown(4f, true);
        _sion.Spells[5].SetCooldown(4f, true);
    }

    public void OnUpdate(Buff buff, float diff)
    {
        // Delay the death animation by one client-settle window (~100ms) instead of playing it on
        // the death tick. The controlling player predicts + locks its own last action anim (an
        // auto-attack/cast) locally; the client drops any S2C_PlayAnimation that arrives while an
        // animation is locked (obj_AI_Base_PImpl_Int::OnNetworkPacket -> IsAnimationLocked() early
        // return), so a death-tick Passive_Death is silently dropped ONLY on the owner (remote
        // viewers have no local lock and show it fine). Waiting a few ticks lets the predicted lock
        // clear so the play is accepted. Flags = Lock only (0x01), matching Riot's replay exactly;
        // the client masks packet bits 5-7 (PlaybackFlagsFromPacketFlags reads only bits 0-3).
        ExecutePeriodically(buff.BuffVars, "SionPassiveSoundStart", 100f, false, 1,
            () => { PlayAnimation(buff.TargetUnit, "Passive_Death", 1.7f, 0, 1, AnimationFlags.Lock); });
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        unit.SetStatus(StatusFlags.Targetable, true);
        unit.SetStatus(StatusFlags.Invulnerable, false);
        unit.SetStatus(StatusFlags.CanMove, true);
        unit.SetStatus(StatusFlags.CanAttack, true);
        unit.SetStatus(StatusFlags.CanCast, true);
        StopAnimation(unit, "Passive_Death", StopAnimationFlags.IgnoreLock);
        AddBuff("SionPassiveZombie", 60f, 1, ownerSpell, _sion, _sion);
        AddBuff("SionPassiveSoundEnd", 60f, 1, ownerSpell, unit, _sion);
        SealSpellSlot(_sion, SpellSlotType.SummonerSpellSlots, 0, SpellbookType.SPELLBOOK_SUMMONER, true);
        SealSpellSlot(_sion, SpellSlotType.SummonerSpellSlots, 1, SpellbookType.SPELLBOOK_SUMMONER, true);
        SealSpellSlot(_sion, SpellSlotType.BluePillSlot, 0, SpellbookType.SPELLBOOK_CHAMPION, true);
    }
}