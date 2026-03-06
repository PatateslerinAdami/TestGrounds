using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class ZedShuriken : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        private ObjAIBase _owner;
        private Minion _wShadow;

        private Minion wShadow
        {
            get
            {
                if (_wShadow == null)
                {
                    var sp = _owner.GetSpell("ZedShadowDashMissile");
                    if (sp?.Script is ZedShadowDashMissile dashScript)
                    {
                        _wShadow = dashScript.shadow;
                    }
                }
                return _wShadow;
            }
        }

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            owner.StopMovement(networked: false);// dont know why in ZedShuriken.ini haveCanMoveWhileChanneling = 1 but didnt wanna edit the ini.

            if (wShadow != null)
            {
                FaceDirection(end, wShadow);
                wShadow.PlayAnimation("Spell1", timeScale: 1.5f);
            }
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;

            var targetPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            var maxRange = spell.GetCurrentCastRange();
            var zedDir = Vector2.Normalize(targetPos - owner.Position);
            var zedEndPos = owner.Position + (zedDir * maxRange);

            SpellCast(owner, 1, SpellSlotType.ExtraSlots, zedEndPos, zedEndPos, true, Vector2.Zero);

            if (owner.HasBuff("ZedWQue") || owner.HasBuff("ZedWHandler2"))
            {
                if (wShadow != null)
                {
                    var shadowDir = Vector2.Normalize(targetPos - wShadow.Position);
                    var shadowEndPos = wShadow.Position + (shadowDir * maxRange);
                    CreateCustomMissile(owner, "ZedShurikenMisOne", wShadow.Position, shadowEndPos, new MissileParameters { Type = MissileType.Circle });
                }
            }
        }
    }

    public class ZedShurikenMisOne : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters { Type = MissileType.Circle },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            var ad = owner.Stats.AttackDamage.Total * spell.SpellData.AttackDamageCoefficient;
            var ap = owner.Stats.AbilityPower.Total * spell.SpellData.MagicDamageCoefficient;
            var damage = 15 + (spell.CastInfo.SpellLevel * 20) + ad + ap;

            var spellO = owner.GetSpell("ZedShuriken");
            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false, spellO);
            AddParticle(owner, target, "zed_q_tar.troy", target.Position);
        }
    }
}