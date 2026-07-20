using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameMaths;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs;

// Sound-event carrier buffs (client plays material hit sounds off these). Wire: AURA,
// hidden, 0.25s. Caster gets SionQSound{Before,After}Half; hit targets get the *Hit
// variants — slam applies BOTH hit variants, flail only BeforeHalfHit.
public class SionQSoundBeforeHalf : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
}

public class SionQSoundAfterHalf : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
}

public class SionQSoundBeforeHalfHit : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
}

public class SionQSoundAfterHalfHit : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
}