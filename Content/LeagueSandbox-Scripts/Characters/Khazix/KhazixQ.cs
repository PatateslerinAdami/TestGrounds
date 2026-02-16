using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class KhazixQ : ISpellScript
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
            _owner.SetSpell("KhazixQLong", spell.CastInfo.SpellSlot, true, true);
            StopAnimation(_owner, "Hide_Claws", fade: true);
            SetSpell(_owner, "KhazixQEvo", SpellSlotType.ExtraSlots, 7, true);
            _owner.GetSpell("KhazixQEvo").Cast(default, default, null);
        }
    }
    public class KhaziQWLong : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
        };
    }
    public class KhaziQWEvo : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            AutoFaceDirection = false,
        };
    }
}