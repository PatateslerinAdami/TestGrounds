using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class KhazixW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            IsDamagingSpell = true,
            TriggersSpellCasts = true
        };
        ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }
        public void OnSpellEvolve(Spell spell)
        {
            _owner.SetSpell("KhazixWLong", spell.CastInfo.SpellSlot, true, true);
            StopAnimation(_owner, "Hide_Spikes", fade: true);
            SetSpell(_owner, "KhazixWEvo", SpellSlotType.ExtraSlots, 6, true);
            _owner.GetSpell("KhazixWEvo").Cast(default, default, null);
        }
    }
    public class KhazixWLong : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
        };
    }
    public class KhazixWEvo : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            AutoFaceDirection = false,
        };
    }
}