using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

// Tibbers is MELEE — its auto-attack must NOT declare a missile. With MissileParameters set, Spell.cs
// FinishCasting both applies the melee instant hit (the IsMelee branch → ApplyEffects + AutoAttackHit) AND
// creates the script-declared missile (the trailing `if (MissileParameters != null) CreateSpellMissile()`),
// so a redundant Target missile flew to the target and replayed the hit FX (globalhit_physical) shortly
// after each swing — the "FX between auto-attacks" bug. Melee AAs hit instantly and the on-hit FX is
// client-side from the spell data. (Ranged units like Annie keep their missile — correct for them.)
public class AnnieTibbersBasicAttack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true,
    };
}

public class AnnieTibbersBasicAttack2 : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true,
    };
}