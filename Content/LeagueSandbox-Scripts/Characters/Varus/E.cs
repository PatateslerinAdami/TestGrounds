using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class VarusE : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
        };

        private ObjAIBase _owner;
        private Spell _spell;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
        }
        public void OnSpellPostCast(Spell spell)
        {
            var targetPos = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);

            var vars = new BuffVariables();
            vars.Set("TargetX", targetPos.X);
            vars.Set("TargetY", targetPos.Y);

            AddBuff("VarusEZoneTracker", 5.0f, 1, spell, _owner, _owner, false, null, vars);
        }
    }
    public class VarusEMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Arc
            },
            IsDamagingSpell = true
        };
        ObjAIBase _owner;
        IEventSource source;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            //ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
            _owner = owner;
        }
    }
    public class VarusEMissileDummy : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Arc
            },
            IsDamagingSpell = true
        };
    }
}