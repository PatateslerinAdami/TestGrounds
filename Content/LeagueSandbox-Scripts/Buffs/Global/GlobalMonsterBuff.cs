using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    // Pre-S5 (S1-S3) additive jungle camp scaling buff (healthPerMinute etc.). NOT used in
    // patch 4.20: jungle scaling on every map is driven by the per-map MonsterDataTable
    // (baseStat x multiplier[averageChampionLevel]) applied in NeutralMinionSpawn.SpawnCamp.
    // Kept as a stub in case a genuine pre-S5 map ever needs the old time-based model.
    internal class GlobalMonsterBuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        Buff thisBuff;
        AttackableUnit owner;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            thisBuff = buff;
            owner = unit;
        }

        public void OnUpdate(Buff buff, float diff)
        {
        }
    }
}
