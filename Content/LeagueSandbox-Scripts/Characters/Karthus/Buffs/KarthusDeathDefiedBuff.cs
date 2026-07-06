using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// Karthus "Death Defied Buff" — the 7s keep-alive that holds the zombie state (S1 DeathDefiedBuff).
// Faithful Karthus is a 0-HP GHOST: health bar hidden, untargetable, immobile, but he can still cast.
// Health is NOT restored — it stays at its death value (0); replay-verified Karthus zombie HP = 0
// (rlp f3ad103e: health bar hidden the whole time, S2C_ShowHealthBar show=0 at death / show=1 at the
// +7083ms boundary). Visible-bar zombies (Sion) instead set a positive decaying health pool — that
// is champion-specific, not the generic default.
//
// Real-death transition is PACKET-driven: at the +7083ms boundary the server sends NPC_ForceDead
// (0x1B) (plus FX_Kill, NPC_BuffRemove2, ShowHealthBar(show)) — that drives the client's
// DoForceDead → SetDeathScreen(true) + HUD death timer. EndZombie() sends the NPC_ForceDead.
internal class KarthusDeathDefiedBuff : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        PersistsThroughDeath = true,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        // 0-HP ghost: leave health at its death value (0) — do NOT restore it. Hide the health bar
        // and make him an untargetable, immobile ghost (CanCast is left intact so he can keep
        // casting during death-defied). Slot-sealing / debuff-clearing from S1 omitted (cosmetic).
        HideHealthBar(unit, hide: true);
        unit.SetStatus(StatusFlags.Targetable, false);
        unit.SetStatus(StatusFlags.CanMove, false);
        unit.SetStatus(StatusFlags.CanAttack, false);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        // Death-defied ended (real death). Restore the flags so the respawned champion is normal,
        // re-show the bar (replay: S2C_ShowHealthBar show=1 here), then finalize the death —
        // EndZombie sends NPC_ForceDead → gray screen + HUD death timer; respawn follows.
        unit.SetStatus(StatusFlags.Targetable, true);
        unit.SetStatus(StatusFlags.CanMove, true);
        unit.SetStatus(StatusFlags.CanAttack, true);
        HideHealthBar(unit, hide: false);
        unit.EndZombie();
    }
}
