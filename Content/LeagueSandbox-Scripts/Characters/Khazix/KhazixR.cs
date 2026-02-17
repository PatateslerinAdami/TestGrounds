using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class KhazixR : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            IsDamagingSpell = true,
            TriggersSpellCasts = true
        };
        ObjAIBase _owner;
        Spell _spell;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
            ApiEventManager.OnStatModified.AddListener(this, owner, CheckMoveSpeed);
        }
        public void OnSpellPostCast(Spell spell)
        {
            AddBuff("KhazixRStealth", 2f, 1, spell, _owner, _owner);
            spell.SetCooldown(0.5f, true);
        }
        private void OnLevelUpSpell(Spell spell)
        {
            _owner.Stats.EvolvePoints++;
        }
        public void OnSpellEvolve(Spell spell)
        {
            SetSpell(_owner, "KhazixREvo", SpellSlotType.ExtraSlots, 8, true);
            _owner.GetSpell("KhazixREvo").Cast( default, default, null);
            var champ = _owner as Champion;
            NotifyChangeSlotSpellData(champ.ClientId, _owner, 3, ChangeSlotSpellDataType.IconIndex, newIconIndex: 1);
            ApiEventManager.OnEnterGrass.AddListener(this, _owner, OnEnter);
        }
        public void OnEnter(AttackableUnit unit)
        {
            AddBuff("KhazixRStealth", 2f, 1, _spell, _owner, _owner);
        }
        public void CheckMoveSpeed(AttackableUnit unit, StatsModifier modifier)
        {
            if (!modifier.MoveSpeed.StatModified) return;

            float speed = unit.GetMoveSpeed();
            bool isFast = speed > 400.0f;
            var anims = new Dictionary<string, string> { { "Run", "Run_fast" } };

            if (isFast)
            {
                unit.SetAnimStates(anims, "HighSpeed");
            }
            else
            {
                unit.RemoveAnimStates("HighSpeed");
            }
        }
    }
    public class KhazixREvo : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            AutoFaceDirection = false,
        };
    }
}
namespace Buffs
{
    class KhazixRStealth : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INVISIBILITY,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.EnterStealth();
            AddParticlePos(unit, "khazix_base_r_cas.troy", unit.Position, unit.Position, lifetime: 3f);
            StatsModifier.MoveSpeed.PercentBonus = 0.50f;
            unit.AddStatModifier(StatsModifier);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.ExitStealth();
        }
    }
}
