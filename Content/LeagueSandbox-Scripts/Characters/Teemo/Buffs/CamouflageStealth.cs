using System.Threading;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class CamouflageStealth : IBuffGameScript {
    private ObjAIBase _teemo;
    private Fade _fade;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INVISIBILITY,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _teemo = buff.SourceUnit;
        SetStatus(_teemo, StatusFlags.Stealthed, true);
        FadeInColorFadeEffect(_teemo, 0, 0, 50, 0.25f, 0.2f);
        _fade =PushCharacterFade(_teemo, 0.2f, 0.2f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _teemo.SetStatus(StatusFlags.Stealthed, false);
        FadeOutColorFadeEffect(_teemo, 0.25f);
        PopCharacterFade(_teemo, _fade);
    }
}