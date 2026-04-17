using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ElixirOfIron : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            // TODO
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            AddBuff("ElixirOfIron", 180f, 1, spell, owner, owner);
        }
    }
}


namespace Buffs
{
    public class ElixirOfIron : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private ObjAIBase _owner;
        private Spell _spell;


        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = ownerSpell.CastInfo.Owner;
            _spell = ownerSpell;

            StatsModifier.Size.PercentBonus += 0.25f;
            StatsModifier.Tenacity.PercentBonus += 0.25f;
            StatsModifier.SlowResistPercent += 0.25f; // or should it be 25f?
            //TODO: add the trail that boosts ally movement speed

            unit.AddStatModifier(StatsModifier);
        }
    }
}