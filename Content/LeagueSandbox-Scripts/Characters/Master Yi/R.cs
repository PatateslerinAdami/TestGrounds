using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class Highlander : ISpellScript {
    private ObjAIBase _masterYi;
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell = false
    };
    public void OnActivate(ObjAIBase owner, Spell spell) {
        _masterYi = owner;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        
    }

    private void OnLevelUpSpell(Spell spell) {
        if (spell.CastInfo.SpellLevel != 1) return;
        ApiEventManager.OnKill.AddListener(this, _masterYi, OnKill);
        ApiEventManager.OnAssist.AddListener(this, _masterYi, OnAssist);
        ApiEventManager.OnLevelUpSpell.RemoveListener(this);
    }

    private void OnKill(DeathData data) {
        if (data.Unit is not Champion) return;
        for (short i = 0; i < 4; i++) {
            _masterYi.Spells[i].LowerCooldown(_masterYi.Spells[i].CurrentCooldown * 0.7f);
        }
    }

    private void OnAssist(ObjAIBase assistant, DeathData data) {
        if (data.Unit is not Champion) return;
        for (short i = 0; i < 4; i++) {
            _masterYi.Spells[i].LowerCooldown(_masterYi.Spells[i].CurrentCooldown * 0.7f);
        }
    }
    
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        AddBuff("Highlander", 10.0f, 1, spell, owner, owner);
    }
}