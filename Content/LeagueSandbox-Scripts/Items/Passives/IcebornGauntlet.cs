using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_3025 : IItemScript
{
    private ObjAIBase _owner = null!;
    private const int ItemId = 3025;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner)
    {
        _owner = owner;
        SpellbladeManager.Register(owner, ItemId);

        Enumerable.Range(0, 4)
            .Where(slot => _owner.Spells.ContainsKey((short)slot))
            .Select(slot => _owner.Spells[(short)slot])
            .ToList()
            .ForEach(spell => ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellCast));
    }

    public void OnDeactivate(ObjAIBase owner)
    {
        SpellbladeManager.Unregister(owner, ItemId);
        ApiEventManager.RemoveAllListenersForOwner(this);
        _owner = null!;
    }

    public void OnUpdate(float diff)
    {
    }

    private void OnSpellCast(Spell spell)
    {
        SpellbladeManager.TryArmSpellblade(_owner, spell);
    }
}
