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

internal class JudicatorIntervention : IBuffGameScript
{
    private ObjAIBase _kayle;
    private Particle _p1, _p2, _p3;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.INVULNERABILITY,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _kayle = buff.SourceUnit;
        SetStatus(unit, StatusFlags.Invulnerable, true);
        PlayAnimation(_kayle, "Spell4", 0, 0, 1, AnimationFlags.NoBlend | AnimationFlags.Junk6 | AnimationFlags.Junk7);
        if (unit == _kayle)
        {
            _p1 = SpellEffectCreate("eyeforaneye_self.troy",_kayle, unit, unit,
                lifetime: buff.Duration, boneName: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.SimulateWhileOffScreen | FXFlags.PARDriven);
        }
        else
        {
            _p1 = SpellEffectCreate("eyeforaneye_cas.troy", _kayle, unit, unit, keywordObject: _kayle, boneName: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.SimulateWhileOffScreen);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
        SetStatus(unit, StatusFlags.Invulnerable, false);
    }
}