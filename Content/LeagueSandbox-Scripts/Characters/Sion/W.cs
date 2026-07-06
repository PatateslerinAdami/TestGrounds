using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class SionW : ISpellScript
    {
        private ObjAIBase _sion;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            DoesntBreakShields = true,
            TriggersSpellCasts = true,
            CastingBreaksStealth = false,
            IsDamagingSpell = true
        };

        

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        }

        private void OnLevelUpSpell(Spell spell)
        {
            if (_sion.HasBuff("SionWPassive"))return;
            AddBuff("SionWPassive", 250000f, 1, spell, _sion, _sion);
        }
        
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            AddParticleTarget(_sion, _sion, "Sion_Base_W_Precas.troy", _sion, size: 0.5f, bone: "C_BUFFBONE_GLB_CHEST_LOC", flags: FXFlags.SimulateWhileOffScreen);
        }

        public void OnSpellCast(Spell spell)
        {
            AddParticleTarget(_sion, _sion, "Sion_Base_W_Cas.troy", _sion, size: 0.5f, bone: "C_BUFFBONE_GLB_CHEST_LOC",  flags: FXFlags.SimulateWhileOffScreen);
            AddBuff("SionWShieldStacks", 6f, 1, spell, _sion, _sion);
            AddBuff("SionW", 6f, 1, spell, _sion, _sion);
        }

        
    }
    
    
    public class SionWDetonate : ISpellScript
    {
        private ObjAIBase _sion;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            DoesntBreakShields = true,
            TriggersSpellCasts = false,
            CastingBreaksStealth = false,
            IsDamagingSpell = true
        };

        

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            AddBuff("SionWDetonate", 1f, 1, spell, _sion, _sion);
            // No sound here: SionWShieldStacks.OnDeactivate adds SionWSoundExplosion for EVERY
            // shield end (recast-detonate, break AND expiry — wire: 60 adds vs 59 shield ends
            // in the test replay), so the recast only needs to end the buff. Adding it here
            // too would double the sound on recast.
            RemoveBuff(_sion, "SionWShieldStacks");
        }
    }
}