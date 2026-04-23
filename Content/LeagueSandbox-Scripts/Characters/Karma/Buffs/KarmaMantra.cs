using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;


namespace Buffs
{
    internal class KarmaMantra: IBuffGameScript {
        private ObjAIBase _karma;
        private Particle  _p1, _p2;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _karma                               =  ownerSpell.CastInfo.Owner;
            ownerSpell.SetCooldown(0f, true);
            _p1 = AddParticleTarget(_karma, _karma, "Karma_Base_R_activate", _karma, buff.Duration);
            _p2 = AddParticleTarget(_karma, _karma, "Karma_Base_R_activate_overhead", _karma, buff.Duration);
            ApiEventManager.OnSpellPostCast.AddListener(this, _karma.GetSpell("KarmaQ"), OnSpellCast);
            ApiEventManager.OnSpellPostCast.AddListener(this, _karma.GetSpell("KarmaSpiritBind"), OnSpellCast);
            ApiEventManager.OnSpellPostCast.AddListener(this, _karma.GetSpell("KarmaSolKimShield"), OnSpellCast); 
        }

        private void OnSpellCast(Spell spell) {
            RemoveBuff(_karma,"KarmaMantra");
        }

        public void OnUpdate(float diff) {
            SealSpellSlot(_karma, SpellSlotType.SpellSlots, 3, SpellbookType.SPELLBOOK_CHAMPION, true);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            SealSpellSlot(_karma, SpellSlotType.SpellSlots, 3, SpellbookType.SPELLBOOK_CHAMPION, false);
            RemoveParticle(_p1);
            RemoveParticle(_p2);
            ownerSpell.SetCooldown(ownerSpell.CastInfo.Cooldown, true);
        }
    }
}