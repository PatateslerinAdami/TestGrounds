using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class RivenFeint : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        ObjAIBase _owner;
        Vector2 _end;
        float dashRange;
        public void OnActivate(ObjAIBase owner, Spell spell) 
        {
            _owner = owner;
            dashRange = spell.SpellData.CastRangeDisplayOverride;
        }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _end = end;
        }
        public void OnSpellPostCast(Spell spell)
        {
            AddBuff("RivenPassiveAABoost", 5f, 1, spell, _owner, _owner, false);
            AddBuff("RivenFeint", 1.5f, 1, spell, _owner, _owner);
            FaceDirection(_end, _owner, true);
            var trueCoords = GetPointFromUnit(_owner, dashRange);
            PlayAnimation(_owner, "Spell3", 0.25f);
            Dash(_owner, trueCoords, 1450, keepFacing: true, lockActions: false);
        }
    }
}