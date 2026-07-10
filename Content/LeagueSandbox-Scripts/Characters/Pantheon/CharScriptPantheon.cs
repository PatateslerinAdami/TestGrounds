using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptPantheon : ICharScript
{
    private ObjAIBase _pantheon;

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _pantheon = owner;
        ApiEventManager.OnSpellCast.AddListener(this, owner.GetSpell("PantheonQ"), OnSpellsCast);
        ApiEventManager.OnSpellCast.AddListener(this, owner.GetSpell("PantheonE"), OnSpellsCast);
        ApiEventManager.OnSpellChannel.AddListener(this, owner.GetSpell("PantheonRJump"), OnSpellsCast);
        ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnSpellsCast);
    }

    private void OnSpellsCast(Spell spell)
    {
        AddBuff("PantheonPassiveCounter", 25000, 1, spell, _pantheon, _pantheon, true);
    }
}