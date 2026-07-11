using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells
{
    public class Terrify : ISpellScript
    {
        private ObjAIBase _fiddleSticks;
        private AttackableUnit _target;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true,
        };
        
        public void OnActivate(ObjAIBase owner, Spell spell) {
            _fiddleSticks = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
            _target = target;
        }
        
        public void OnSpellPostCast(Spell spell)
        {
            SpellEffectCreate("Terrify_tar.troy", _fiddleSticks, _target, _target, boneName: "C_Buffbone_Glb_Head_Loc", flags: FXFlags.SimulateWhileOffScreen);
            SpellEffectCreate("Terrify_cas.troy", _fiddleSticks, _fiddleSticks, null, flags: FXFlags.SimulateWhileOffScreen);
            var duration = 1.25f + 0.25f * (spell.CastInfo.SpellLevel - 1);
            AddBuff("FleeSlow", duration, 1, spell, _target, _fiddleSticks);
            AddBuff("Flee", duration, 1, spell, _target, _fiddleSticks);
        }
    }
}
