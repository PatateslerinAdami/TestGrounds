using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptAnnie : ICharScript
{
    private ObjAIBase _annie;
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null)
    {
        _annie = owner;
        ApiEventManager.OnHitUnit.AddListener(this, owner,  OnHit);
    }

    private void OnHit(DamageData data)
    {
        var particles = _annie.SkinID switch
        {
            1 => "Annie_skin02_BasicAttack_tar.troy",
            5 => "Annie_skin05_BasicAttack_tar.troy",
            _ => "Annie_BasicAttack_tar.troy"
        };
        SpellEffectCreate(particles, _annie, data.Target, data.Target, boneName: "", flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
    }
}