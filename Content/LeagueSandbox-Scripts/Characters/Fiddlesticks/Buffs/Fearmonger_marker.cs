using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// S1 DrainChannel.lua ChannelingStart: BBSpellBuffAdd on Owner, BuffName "Fearmonger_marker",
// BuffType = BUFF_Heal (5s, REPLACE_EXISTING, MaxStack 1). Owner-side heal marker; its
// Fearmonger_marker.lua declares AutoBuffActivateEffect = "Fearmonger_cas.troy", replicated here.
// The heal itself is applied per-tick by the DrainChannel spell (S1 GlobalDrain mechanism).
public class Fearmonger_marker : IBuffGameScript
{
    private Particle _castFx;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType    = BuffType.HEAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _castFx = AddParticleTarget(buff.SourceUnit, buff.SourceUnit, "Fearmonger_cas.troy", unit, buff.Duration);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        if (_castFx != null)
        {
            RemoveParticle(_castFx);
            _castFx = null;
        }
    }
}
