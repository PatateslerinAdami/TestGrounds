using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells
{
    public class ShacoBoxSpell : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            MissileParameters = new MissileParameters { Type = MissileType.Target },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnHit);
        }

        private void OnHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            missile?.SetToRemove();
            var box = spell.CastInfo.Owner as Minion;
            if (box == null || target == null || target.IsDead || box.Owner == null) return;

            target.TakeDamage(box.Owner, box.Stats.AttackDamage.Total, DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_PET, false);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.RemoveListener(this);
        }
    }

    public class ShacoBoxSpell2 : ShacoBoxSpell { }
    public class ShacoBoxBasicAttack : ShacoBoxSpell { }
    public class ShacoBoxAttackMissile : ShacoBoxSpell { }
}