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
using GameServerLib.GameObjects.AttackableUnits;
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
            IsDamagingSpell = true,
            AutoFaceDirection = false
        };
        
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
        }

        public void OnSpellPostCast(Spell spell)
        {
            SpellEffectCreate("Sion_Base_W_Cas.troy", _sion, _sion, _sion, scale: 0.5f,
                boneName: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.SimulateWhileOffScreen);
            SpellEffectCreate("Sion_Base_W_Precas.troy", _sion, _sion, _sion, scale: 0.5f,
                boneName: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.SimulateWhileOffScreen);
            // SionWShieldStacks owns the whole W lifecycle: shield, the SionW→SionWDetonate slot
            // swap (arm/restore) and the recast-lockout. No separate SionW buff — Riot never
            // replicates one; the swap it drives goes out via ChangeSlotSpellData, not a buff.
            AddBuff("SionWShieldStacks", 6f, 1, spell, _sion, _sion);
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
            IsDamagingSpell = true,
            AutoFaceDirection = false
        };


        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            // Detonate = just end the shield buff. Everything else (AoE damage, explosion sound,
            // slot restore) is driven by SionWShieldStacks.OnDeactivate, which runs for EVERY
            // shield end (recast-detonate, break AND expiry — wire: 60 SionWSoundExplosion adds vs
            // 59 shield ends in the test replay). No SionWDetonate marker buff: Riot never sends one
            // (SionWDetonate is a spell, replicated via ChangeSlotSpellData, not a buff).
            RemoveBuff(_sion, "SionWShieldStacks");
        }
    }

    public class SionPassiveSpeed : ISpellScript
    {
        private ObjAIBase _sion;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = false,
            DoesntBreakShields = true,
            TriggersSpellCasts = false,
            CastingBreaksStealth = false,
            IsDamagingSpell = true,
            AutoFaceDirection = false
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            AddBuff("SionPassiveSpeed", 3f, 1, spell, _sion, _sion);
        }
    }
}