using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class KhazixE : ISpellScript
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
            _owner.SetSpell("KhazixELong", spell.CastInfo.SpellSlot, true, true);
            StopAnimation(_owner, "Hide_Wings", fade: true);
            SetSpell(_owner, "KhazixEEvo", SpellSlotType.ExtraSlots, 4, true);
            _owner.GetSpell("KhazixEEvo").Cast(default, default, null);
        }
    }
    public class KhazixELong : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
        };
    }
    public class KhazixEEvo : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            AutoFaceDirection = false,
        };
    }
}