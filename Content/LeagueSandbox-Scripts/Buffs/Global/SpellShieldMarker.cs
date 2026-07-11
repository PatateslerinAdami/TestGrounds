using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    // Generic spell-shield break marker — Riot global buff name from BBBreakSpellShields
    // (BuildingBlocksBase.lua:3471; PreloadSpell'd by every map LevelScript). Replay-verified:
    // the ENGINE break never puts this on the wire (0 occurrences across 3 replays) — the engine
    // gate lives in Spell.ApplyEffects → AttackableUnit.ConsumeSpellShield, wire-silent. This buff
    // exists only for BB-script parity: if a script ever adds it, spell-shield buffs (SivirE)
    // consume it AND themselves via their OnUnitBuffActivated hook. Carries no effect of its own.
    internal class SpellShieldMarker : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; }
    }
}
