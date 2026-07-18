using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class DarkBindingMissile : IBuffGameScript
{
    private ObjAIBase _morgana;
    private Particle _snareParticle;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.SNARE,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _morgana = buff.SourceUnit;
        var particleName = _morgana.SkinID switch
        {
            4 => "Morgana_Blackthorn_DarkBinding_tar.troy",
            5 => "Morgana_Skin05_Q_Tar.troy",
            6 => "Morgana_Skin06_Q_Tar.troy",
            _ => "Morgana_Base_Q_Tar.troy",
        };
        _snareParticle = SpellEffectCreate(particleName, _morgana, unit, unit, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_snareParticle);
    }
}