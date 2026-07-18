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

internal class SoulShackles : IBuffGameScript
{
    private ObjAIBase _morgana;
    private Region _bubble;
    private Particle _p1, _p2, _p3;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _morgana = buff.SourceUnit;

        if (_morgana.SkinID != 5)
        {
            var particleNameBuf = _morgana.SkinID switch
            {
                4 => "Morgana_Blackthorn_SoulShackle_buf.troy",
                _ => "SoulShackle_buf.troy"
            };
            _p1 = SpellEffectCreate(particleNameBuf, _morgana, unit, null, lifetime: buff.Duration,
                flags: FXFlags.SimulateWhileOffScreen);
        }

        var particleNameTar = _morgana.SkinID switch
        {
            4 => "Morgana_Blackthorn_SoulShackle_tar.troy",
            5 => "Morgana_Skin05_R_Tar_Timer.troy",
            6 => "Morgana_Skin06_R_Tar.troy",
            _ => "Morgana_Base_R_Tar.troy"
        };

        _p2 = SpellEffectCreate(particleNameTar, _morgana, unit, null, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);

        var particleNameBeam = _morgana.SkinID switch
        {
            5 => "Morgana_Skin05_R_Mis.troy",
            6 => "Morgana_Skin06_R_Beam.troy",
            _ => "Morgana_Base_R_Beam.troy"
        };
        _p3 = SpellEffectCreate(particleNameBeam, _morgana, _morgana, unit, lifetime: buff.Duration, boneName: "Spine",
            targetBoneName: "C_Buffbone_Glb_Chest_Loc", flags: FXFlags.SimulateWhileOffScreen);
        _bubble = AddUnitPerceptionBubble(unit, unit.Stats.Size.Total, buff.Duration, _morgana.Team, true, unit);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
        _bubble?.SetToRemove();
    }
}