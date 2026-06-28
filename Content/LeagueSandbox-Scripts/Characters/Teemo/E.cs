using System.Linq;
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
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class ToxicShot : ISpellScript
{
    private ObjAIBase _teemo;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = false,
        IsDamagingSpell = false,
        NotSingleTargetSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _teemo = owner;
        _spell = spell;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
    }

    private void OnLevelUpSpell(Spell spell)
    {
        AddBuff("ToxicShot", 250000f, 1, _spell, _teemo, _teemo, infiniteduration: true);
    }
}

public class ToxicShotAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Target
        },
        IsDamagingSpell = true
    };
}