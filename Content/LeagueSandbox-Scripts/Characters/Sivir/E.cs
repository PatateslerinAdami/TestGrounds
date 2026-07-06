using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SivirE : ISpellScript {

    private ObjAIBase _sivir;
    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = true,
        TriggersSpellCasts = true,
        IsDamagingSpell = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _sivir = owner;
    }

    public void OnSpellCast(Spell spell)
    {
        AddBuff("SivirE", 1.5f, 1, spell, _sivir, _sivir);
    }
}