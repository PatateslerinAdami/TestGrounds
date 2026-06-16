using LeagueSandbox.GameServer.GameObjects.SpellNS;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace CharScripts;

public class CharScriptOldion : ICharScript
{
    private ObjAIBase _oldion;
    private readonly float[] _blockAmounts = { 30, 30, 30, 30, 30, 30, 40, 40, 40, 40, 40, 40, 50, 50, 50, 50, 50, 50 };
    private const float BlockChance = 0.40f;
    private static readonly Random _random = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null)
    {
        _oldion = owner;
        ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage, false);
    }

    private void OnTakeDamage(DamageData data)
    {
        if (_oldion == null || data.Damage <= 0) return;
        if (_random.NextDouble() > BlockChance) return;

        int level = _oldion.Stats.Level;
        int levelIndex = Math.Clamp(level - 1, 0, 17);
        float blockAmount = _blockAmounts[levelIndex];
        float actualBlock = Math.Min(blockAmount, data.Damage);
        data.Damage -= actualBlock;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell = null) { }
    public void OnUpdate(float diff) { }
}
