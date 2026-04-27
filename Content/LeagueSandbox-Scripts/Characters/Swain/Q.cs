using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SwainDecrepify : ISpellScript
{
    private ObjAIBase _swain;
    private AttackableUnit _target;
    private Vector2 _ravenPos;
    private Minion _beatrix;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _swain = owner;
        _target = target;
        _ravenPos = GetPointFromUnit(_swain, 100, 0);
        PlayAnimation(_swain, "Spell1");
        _beatrix = AddMinion(_swain, "SwainBeam", "HiddenMinion", _ravenPos, _swain.Team, _swain.SkinID, true, false,
            false, SpellDataFlags.NoClick);
        FaceDirection(_target.Position, _beatrix, true);
        AddBuff("SwainDecrepify", 3f, 1, spell, _beatrix, _swain);
        AddBuff("SwainHasBeatrix", 3f, 1, spell, _swain, _swain);
    }

    public void OnSpellCast(Spell spell)
    {
    }

    public void OnSpellPostCast(Spell spell)
    {
        if (_swain.Model.Equals("Swain"))
        {
            _swain.ChangeModel("SwainNoBird");
        }
    }

    public void OnUpdate(float diff)
    {
    }
}