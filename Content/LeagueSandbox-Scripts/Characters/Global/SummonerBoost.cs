using System.Linq;
using System.Numerics;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class SummonerBoost : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false,
            CastingBreaksStealth = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            ItemCleanseHelpers.Cleanse(owner, false);
            foreach (var buff in owner.GetBuffs().ToArray())
            {
                if (buff.Name == "SummonerDot" || buff.Name == "SummonerExhaustDebuff")
                {
                    RemoveBuff(buff);
                }
            }

            AddBuff("SummonerBoost", 3f, 1, spell, owner, owner);
            AddParticleTarget(owner, owner, "summoner_boost.troy", owner, 1f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC", ignoreCasterVisibility: true);
        }
    }
}
