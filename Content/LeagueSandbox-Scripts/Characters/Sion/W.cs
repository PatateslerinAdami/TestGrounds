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
            IsDamagingSpell = true
        };

        

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
            _sion.CharVars.Set("SoulFurnaceStacks", 0f);
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
            ApiEventManager.OnUpdateStats.AddListener(this, _sion, OnUpdateStats);
        }
        
        private void OnLevelUpSpell(Spell spell)
        {
            if (spell.CastInfo.SpellLevel != 1)return;
            ApiEventManager.OnKillUnit.AddListener(this, _sion, OnUnitKill);
        }
        
        private void OnUnitKill(DeathData data)
        {
            // UnitTags is a [Flags] composite (a siege minion is Minion | Minion_Lane |
            // Minion_Lane_Siege), so equality patterns never match a live unit — query with
            // ContainsAny, specific tags first since those units also carry the Minion bit.
            var tags = data.Unit.UnitTags;
            if (tags.ContainsAny(UnitTag.Minion_Lane_Siege | UnitTag.Minion_Lane_Super | UnitTag.Champion
                                 | UnitTag.Champion_Clone | UnitTag.Monster_Large | UnitTag.Monster_Epic))
            {
                _sion.Stats.HealthPoints.FlatBonus =+ 15f;
                _sion.CharVars.Set("SoulFurnaceStacks", _sion.CharVars.GetFloat("SoulFurnaceStacks") + 15f);
            }
            else if (tags.ContainsAny(UnitTag.Minion | UnitTag.Minion_Lane | UnitTag.Ward | UnitTag.Minion_Summon))
            {
                _sion.Stats.HealthPoints.FlatBonus =+ 4f;
                _sion.CharVars.Set("SoulFurnaceStacks", _sion.CharVars.GetFloat("SoulFurnaceStacks") + 4f);
            }
        }

        public void OnSpellCast(Spell spell)
        {
            SpellEffectCreate("Sion_Base_W_Cas.troy",_sion, _sion,  _sion, scale: 0.5f, boneName: "C_Buffbone_Glb_Center_Loc",  flags: FXFlags.SimulateWhileOffScreen);
            SpellEffectCreate("Sion_Base_W_Precas.troy",_sion, _sion,  _sion, scale: 0.5f, boneName: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.SimulateWhileOffScreen);
            // SionWShieldStacks owns the whole W lifecycle: shield, the SionW→SionWDetonate slot
            // swap (arm/restore) and the recast-lockout. No separate SionW buff — Riot never
            // replicates one; the swap it drives goes out via ChangeSlotSpellData, not a buff.
            AddBuff("SionWShieldStacks", 6f, 1, spell, _sion, _sion);
        }
        
        private void OnUpdateStats(AttackableUnit unit, float diff)
        {
            SetSpellToolTipVar(_sion, 0, _sion.CharVars.GetFloat("SoulFurnaceStacks"), SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
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
            _sion.CharVars.Set("SoulFurnaceStacks", 0f);
            ApiEventManager.OnUpdateStats.AddListener(this, _sion, OnUpdateStats);
            ApiEventManager.OnKillUnit.AddListener(this, _sion, OnUnitKill);
        }
        
        private void OnUpdateStats(AttackableUnit unit, float diff)
        {
            SetSpellToolTipVar(_sion, 0, _sion.CharVars.GetFloat("SoulFurnaceStacks"), SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        }
        
        private void OnUnitKill(DeathData data)
        {
            // UnitTags is a [Flags] composite (a siege minion is Minion | Minion_Lane |
            // Minion_Lane_Siege), so equality patterns never match a live unit — query with
            // ContainsAny, specific tags first since those units also carry the Minion bit.
            var tags = data.Unit.UnitTags;
            if (tags.ContainsAny(UnitTag.Minion_Lane_Siege | UnitTag.Minion_Lane_Super | UnitTag.Champion
                                 | UnitTag.Champion_Clone | UnitTag.Monster_Large | UnitTag.Monster_Epic))
            {
                _sion.Stats.HealthPoints.FlatBonus =+ 15f;
                _sion.CharVars.Set("SoulFurnaceStacks", _sion.CharVars.GetFloat("SoulFurnaceStacks") + 15f);
            }
            else if (tags.ContainsAny(UnitTag.Minion | UnitTag.Minion_Lane | UnitTag.Ward | UnitTag.Minion_Summon))
            {
                _sion.Stats.HealthPoints.FlatBonus =+ 4f;
                _sion.CharVars.Set("SoulFurnaceStacks", _sion.CharVars.GetFloat("SoulFurnaceStacks") + 4f);
            }
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
}