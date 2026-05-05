using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class MoveQuick : ISpellScript {
    private ObjAIBase _teemo;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts   = true,
        NotSingleTargetSpell = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _teemo = owner;
        _spell = spell;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage);
    }
    

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        if (_teemo.HasBuff("TeemoMoveQuickSpeed")) { RemoveBuff(_teemo, "TeemoMoveQuickSpeed"); }

        AddBuff("MoveQuick", 3f, 1, spell, owner, owner);
    }
    
    private void OnLevelUpSpell(Spell spell) {
        if (spell.CastInfo.SpellLevel == 1) {
            AddBuff("TeemoMoveQuickSpeed", 10000000000f, 1, spell, _teemo, _teemo, infiniteduration: true);
        }
    }

    private void OnTakeDamage(DamageData damageData) {
        if (IsValidTarget(_teemo, damageData.Attacker, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectTurrets))
            if (_teemo.HasBuff("TeemoMoveQuickSpeed")) { RemoveBuff(_teemo, "TeemoMoveQuickSpeed"); }

        AddBuff("TeemoMoveQuickDebuff", 5f, 1, _spell, _teemo, _teemo);
    }
}