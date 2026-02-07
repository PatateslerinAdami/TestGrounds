using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class Deceive : ISpellScript
    {
        private ObjAIBase _owner;
        private Vector2 _end;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _owner = owner;
            _end = end ;
        }
        
        public void OnSpellPostCast(Spell spell)
        {
            FaceDirection(_end, _owner, true);
            Vector2 direction = _end - _owner.Position;
            float maxRange = spell.SpellData.CastRangeDisplayOverride;
            float distance = direction.Length();
            Vector2 result = distance > maxRange
                ? _owner.Position + Vector2.Normalize(direction) * maxRange
                : _end;
            AddParticle(_owner, null, "JackintheboxPoof2", _owner.Position, lifetime: 2f);
            AddBuff("Deceive", 5f, 1, spell, _owner, _owner);
            _owner.TeleportTo(result.X, result.Y, false);
            
        }
        
    }

}
