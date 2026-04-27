using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TalonNoxianDiplomacy : ISpellScript
{
    private ObjAIBase _talon;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _talon = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        AddBuff("TalonNoxianDiplomacyBuff", 5f, 1, spell, _talon, _talon);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff)
    {
        SetSpellToolTipVar(_talon, 0, _talon.Stats.AttackDamage.FlatBonus,
            SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);

        SetSpellToolTipVar(_talon, 1, _talon.Stats.AttackDamage.FlatBonus * 0.167f,
            SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}

public class TalonNoxianDiplomacyAttack : ISpellScript
{
    
    public SpellScriptMetadata ScriptMetadata { get; private set; } = new()
    {
        IsDamagingSpell = true,
        CastingBreaksStealth = true
    };
}


