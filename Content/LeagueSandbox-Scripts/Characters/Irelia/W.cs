using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class IreliaHitenStyle : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true,
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, LevelUp);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        RemoveBuff(owner, "IreliaHitenStyle");
        AddBuff("IreliaHitenStyleCharged", 6.0f, 1, spell, owner, owner);
    }

    private void LevelUp(Spell spell) {
        if (spell.CastInfo.SpellLevel != 1) return;
        AddBuff("IreliaHitenStyle", 999999f, 1, spell, spell.CastInfo.Owner, spell.CastInfo.Owner, true);
    }
}