using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class KarmaSpiritBind : ISpellScript {
    private ObjAIBase _karma;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _karma = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _karma, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_karma, _karma.HasBuff("KarmaMantra") ? 1 : 0, SpellSlotType.ExtraSlots, true, _target, Vector2.Zero);
        //0 is WnonMantra
        //1 is WMantra
    }
    
    private void OnUpdateStats(AttackableUnit target, float diff) {
        var bonusDmg = 75f + 75f * (_karma.GetSpell("KarmaMantra").CastInfo.SpellLevel - 1);
        SetSpellToolTipVar(_karma, 0, bonusDmg, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }
}

public class KarmaWNonMantra : ISpellScript {
    private ObjAIBase _karma;
    private AttackableUnit _target;
    private Vector2   _endPos;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _karma = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        var variables = new BuffVariables();
        variables.Set("rootDuration", 1f + 0.25f *(_karma.GetSpell("KarmaSpiritBind").CastInfo.SpellLevel - 1));
        AddBuff("KarmaSpiritBind", 2f, 1, _karma.GetSpell("KarmaSpiritBind"), _target, _karma, buffVariables: variables);
    }

    public void OnSpellPostCast(Spell spell) {
        
    }
}

public class KarmaWMantra : ISpellScript {
    private ObjAIBase      _karma;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _karma = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        var variables = new BuffVariables();
        variables.Set("rootDuration", 1f + 0.25f *(_karma.GetSpell("KarmaSpiritBind").CastInfo.SpellLevel - 1));
        variables.Set("isMantra",     true);
        AddBuff("KarmaSpiritBind", 2f, 1, _karma.GetSpell("KarmaSpiritBind"), _target, _karma, buffVariables: variables);
    }

    public void OnSpellPostCast(Spell spell) {
        
    }
}