using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class MalphiteShield : ISpellScript {
    private ObjAIBase _malphite;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _malphite = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        SetSpellToolTipVar(unit, 0, 10f, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.PassiveSpellSlot);
        SetSpellToolTipVar(unit, 1, _malphite.Stats.HealthPoints.Total * 0.1f, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.PassiveSpellSlot);
    }
}