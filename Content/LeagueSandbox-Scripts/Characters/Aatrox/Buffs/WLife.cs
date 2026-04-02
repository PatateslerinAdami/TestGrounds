using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    class AatroxWLife : IBuffGameScript
    {
        private ObjAIBase _owner;
        Particle weaponGlow;

        private static readonly Dictionary<int, string> skins = new()
        {
            { 0, "Aatrox_Base_W_" },
            { 1, "Aatrox_Skin01_W_" },
            { 2, "Aatrox_Skin02_W_" }
        };

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = ownerSpell.CastInfo.Owner;
            SetSpell(_owner, "AatroxW", SpellSlotType.SpellSlots, 1);
            ApiEventManager.OnLaunchAttack.AddListener(this, _owner, OnAttack);

            weaponGlow = AddParticleTarget(_owner, _owner, skins[_owner.SkinID] + "Weaponlife", _owner, bone: "weapon");
            weaponGlow.isInfinite = true;

            string anim = _owner.HasBuff("AatroxR") ? "spell2_ULT" : "Spell2";
            OverrideAnimation(_owner, anim, "Attack3", "AatroxW");
            OverrideAnimation(_owner, anim, "Attack6", "AatroxW");

            if (_owner.hitCount == 2)
            {
                AddBuff("AatroxWONHLifeBuff", 1f, 1, ownerSpell, _owner, _owner, true);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnLaunchAttack.RemoveListener(this, _owner);
            weaponGlow.SetToRemove();

            ClearOverrideAnimation(unit, "Attack3", "AatroxW");
            ClearOverrideAnimation(unit, "Attack6", "AatroxW");

            if (_owner.GetBuffWithName("AatroxWONHLifeBuff") is Buff a)
            {
                a.DeactivateBuff();
            }
        }

        private void OnAttack(Spell spell)
        {
            _owner.hitCount++;
            if (_owner.hitCount == 2)
            {
                AddBuff("AatroxWONHLifeBuff", 1f, 1, spell, _owner, _owner, true);
            }
            if (_owner.hitCount >= 3)
            {
                _owner.hitCount = 0;
                if (_owner.GetBuffWithName("AatroxWONHLifeBuff") is Buff a)
                {
                    a.DeactivateBuff();
                }
            }
        }
    }

    class AatroxWONHLifeBuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var owner = ownerSpell.CastInfo.Owner;
            owner.SetAutoAttackSpell(owner.HasBuff("AatroxR") ? "AatroxBasicAttack6" : "AatroxBasicAttack3", false);
        }
    }
}