using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TrundleTrollSmash : ISpellScript {
    private ObjAIBase      _trundle;
    private Spell          _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _trundle = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _trundle, OnUpdateStats);
    }

    public void OnSpellPostCast(Spell spell) {
        AddBuff("TrundleTrollSmash", 8f, 1, spell, _trundle, _trundle);
    }

    private void OnUpdateStats(AttackableUnit owner, float diff) {
        var ad  = _trundle.Stats.AttackDamage.Total * (1f + 0.05f * (_spell.CastInfo.SpellLevel - 1));
        SetSpellToolTipVar(_trundle, 0, ad, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}

public class TrundleQ : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true
    };
}
