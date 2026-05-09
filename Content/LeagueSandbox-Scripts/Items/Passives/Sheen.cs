using GameServerCore.Scripting.CSharp;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.GameObjects;


namespace ItemPassives;

public class ItemID_3057 : IItemScript {
    private ObjAIBase     _owner = null!;
    private const int ItemId = 3057;
    
    public         StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        SpellbladeManager.Register(owner, ItemId);
        Enumerable.Range(0, 4)
            .Where(slot => _owner.Spells.ContainsKey((short)slot))
            .Select(slot => _owner.Spells[(short)slot])
            .ToList()
            .ForEach(spell => ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellCast));
        
    }

    public void OnSpellCast(Spell spell) {
        SpellbladeManager.TryArmSpellblade(_owner, spell);
    }

    public void OnDeactivate(ObjAIBase owner) {
        SpellbladeManager.Unregister(owner, ItemId);
        ApiEventManager.RemoveAllListenersForOwner(this);
        _owner = null!;
    }
}
