using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using System.Collections.Generic;
using LeagueSandbox.GameServer.GameObjects;

namespace Spells
{
    public class FioraFlurry : ISpellScript
    {
        private Spell _spell;
        private ObjAIBase _fiora;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _fiora = owner;
        }

        public void OnSpellCast(Spell spell)
        {
            _fiora.CancelAutoAttack(true, false);
            _spell = spell;
            _fiora = spell.CastInfo.Owner as Champion;
            AddBuff("FioraFlurry", 3.0f, 1, spell, _fiora, _fiora);
        }
    }
}