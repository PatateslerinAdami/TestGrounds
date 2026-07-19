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
    public class SionE : ISpellScript
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
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
        }

        public void OnSpellCast(Spell spell)
        {
            SpellEffectCreate("Sion_E_Sound.troy", _sion, null, null, _sion.Position, _sion.Position,
                flags: FXFlags.SimulateWhileOffScreen);
            SpellEffectCreate("Sion_Base_E_Cas.troy", _sion, _sion, _sion, boneName: "L_Clavicle",
                flags: FXFlags.SimulateWhileOffScreen);
        }

        public void OnSpellPostCast(Spell spell)
        {
            SpellCast(_sion, 1, SpellSlotType.ExtraSlots, _sion.Position, GetPointFromUnit(_sion, 800f), true,
                Vector2.Zero);
        }
    }

    public class SionEMissile : ISpellScript
    {
        private ObjAIBase _sion;
        private Spell _spell;
        private VariableTable _variableTable = new VariableTable();

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Arc,
                CanHitEnemies = true,
                CanHitFriends = false,
                CanHitCaster = false,
            },
            NotSingleTargetSpell = false,
            TriggersSpellCasts = false,
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _variableTable.Set("end", end);
            _variableTable.Set("start", start);
            _spell = spell;
        }

        private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
        {
            var mainSpell = _sion.GetSpell("SionE");
            SpellEffectCreate("Sion_Base_E_Tar.troy", _sion, target, target, orientTowards: target.GetPosition3D(),
                flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);
            if (target is Minion or Monster)
            {
                AddBuff("SionESoundMinionColide", 0.25f, 1, mainSpell, target, _sion);
                var distFromCaster = Vector2.Distance(_sion.Position, target.Position);
                var pushDist = 1350 - distFromCaster;
                if (pushDist < 0) pushDist = 0;
                AddParticleTarget(_sion, target, "Sion_Base_E_Minion.troy", _sion, size: 2f,
                    flags: FXFlags.SimulateWhileOffScreen);
                AddBuff("SionEArmorShred", 2.5f, 1, mainSpell, target, _sion);
                AddBuff("SionEMinion", 1f, 1, mainSpell, target, _sion, variableTable: _variableTable);
                missile.SetToRemove();
            }
            else
            {
                AddBuff("SionESlow", 2.5f, 1, mainSpell, target, _sion);
                AddBuff("SionEArmorShred", 2.5f, 1, mainSpell, target, _sion);
                var ap = _sion.Stats.AbilityPower.Total * mainSpell.SpellData.Coefficient;
                var dmg = mainSpell.SpellData.EffectLevelAmount[1][mainSpell.CastInfo.SpellLevel] + ap;
                target.TakeDamage(_sion, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                    DamageResultType.RESULT_NORMAL);
            }
        }
    }
}