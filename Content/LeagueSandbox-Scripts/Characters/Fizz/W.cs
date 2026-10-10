using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FizzSeastonePassive : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true,
            AutoFaceDirection = false
        };

        public void OnSpellPostCast(Spell spell)
        {
            AddBuff("FizzSeastonePassive", 5, 1, spell, spell.CastInfo.Owner, spell.CastInfo.Owner);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            var buff = owner.GetBuffWithName("FizzSeastonePassive");
            if (buff != null && buff.OriginSpell == spell)
            {
                RemoveBuff(buff);
            }
        }
    }

    public class FizzSeastoneTrident : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            IsDeathRecapSource = true
        };
    }

    public class FizzSeastoneTridentActive : FizzSeastoneTrident
    {
    }

    public class FizzSeastoneActive : FizzSeastoneTrident
    {
    }

    public class FizzPassive : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata();
    }
}
