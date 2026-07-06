using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs;

// S1-Pattern: the channel-buff owns the
// particle-FX and the Spell4 looping-animation. Lifecycle is atomic via OnActivate /
// OnDeactivate, so cleanup-ordering races (StopAnimation vs RemoveParticle vs RemoveBuff)
// can't happen when the buff is removed (success or cancel), OnDeactivate runs once
// and tears everything down in the correct order.
class KatarinaRSound : IBuffGameScript
{
    private ObjAIBase _katarina;
    private Spell _ownerSpell;
    private Particle _p;

    public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _katarina = ownerSpell.CastInfo.Owner;
        _ownerSpell = ownerSpell;

        // Particle only the animation lives in KatarinaR.OnSpellCast (cast-start), buff is added
        // in KatarinaR.OnSpellChannel (channel-start, post-windup). Replay (id=66754 t=576358
        // PlayAnimation + id=66760 t=576362 BuffAdd2): cast-start anim, +4ms channel-start buff.
        _p = _katarina.SkinID switch
        {
            9 => AddParticleTarget(_katarina, _katarina, "Katarina_Skin09_R_cas", _katarina, 2.4f),
            _ => AddParticleTarget(_katarina, _katarina, "Katarina_deathLotus_cas", _katarina, 2.4f)
        };
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        // Replay (id=67106 t=578760) StopAnimation: name="" flags=0x33 (Fade|IgnoreLock|Unknown_0x10|Unknown_0x20).
        // Unknown_0x10|Unknown_0x20 are the looped-spell-cast-stop signal; empty name pairs with them, NOT "Spell4".
        StopAnimation(_katarina, "Spell4",
            StopAnimationFlags.FadeOut | StopAnimationFlags.IgnoreLock);
        RemoveParticle(_p);
    }
}
