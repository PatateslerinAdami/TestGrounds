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

public class IreliaHitenStyle : ISpellScript
{
    private ObjAIBase _irelia;
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true,
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _irelia = owner;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, LevelUp);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        RemoveBuff(_irelia, "IreliaHitenStyle");
        AddBuff("IreliaHitenStyleCharged", 6.0f, 1, spell, _irelia, _irelia);
    }

    private void LevelUp(Spell spell) {
        if (spell.CastInfo.SpellLevel != 1) return;
        AddBuff("IreliaHitenStyle", 999999f, 1, spell, _irelia, _irelia, true);
    }
}