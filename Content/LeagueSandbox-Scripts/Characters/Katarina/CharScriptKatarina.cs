using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptKatarina : ICharScript
{
    private ObjAIBase _katarina;

    public void OnActivate(ObjAIBase owner, Spell spell = null)
    {
        _katarina = owner;
        ApiEventManager.OnKill.AddListener(this, owner, OnKill);
        ApiEventManager.OnAssist.AddListener(this, owner, OnAssist);
    }

    private void OnKill(DeathData data)
    {
        if (data.Unit is not Champion) return;
        EmitVoracityProc(isOwnKill: true);
    }

    private void OnAssist(ObjAIBase assistant, DeathData data)
    {
        if (data.Unit is not Champion) return;
        EmitVoracityProc(isOwnKill: false);
    }

    /// <summary>
    /// Replay-verified Voracity wire-pattern (Kat-perspective replay, 22 events × 4 packets):
    /// Q/W/E reset to 0 (per-target), R updated via slot=3 with isSummonerSpell-toggle (KILL
    /// broadcasts, ASSIST per-target). See <see cref="ApiFunctionManager.NotifyVoracityProc"/>
    /// for the full wire-shape documentation.
    /// </summary>
    private void EmitVoracityProc(bool isOwnKill)
    {
        // Server-side cooldown reset (silent — wire packets emitted via NotifyVoracityProc).
        // Q/W/E set to 0 directly (matches Riot wire). R via LowerCooldown to keep our counter
        // formula consistent with the replay-verified `R_last_cast_cd − Δt − 15×takedowns` pattern.
        _katarina.GetSpell("KatarinaQ")?.SetCooldown(0, ignoreCDR: true, silent: true);
        _katarina.GetSpell("KatarinaW")?.SetCooldown(0, ignoreCDR: true, silent: true);
        _katarina.GetSpell("KatarinaE")?.SetCooldown(0, ignoreCDR: true, silent: true);
        var r = _katarina.GetSpell("KatarinaR");
        r?.LowerCooldown(15.0f, silent: true);

        // Wire emit: 4 packets per Voracity event matching Riot's exact shape.
        NotifyVoracityProc(_katarina, isOwnKill, r?.CurrentCooldown ?? 0f);

        // Particle: replay-empirically Riot DOES broadcast `katarina_spell_refresh_indicator.troy`
        // (hash 48865785) as FX_Create_Group on every Voracity event — 21/22 hits in the
        // Kat-perspective replay within 0-27ms of the cooldown packets. `Particle.NormalizeParticleName`
        // auto-appends the `.troy` suffix so our hash matches Riot's wire.
        switch (_katarina.SkinID)
        {
            case 9:
                AddParticle(_katarina, _katarina, "Katarina_P_Cast", _katarina.Position);
                break;
            default:
                AddParticle(_katarina, _katarina, "katarina_spell_refresh_indicator", _katarina.Position);
                break;
        }
    }
}
