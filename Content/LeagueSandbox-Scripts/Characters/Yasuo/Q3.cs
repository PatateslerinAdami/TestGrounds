
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

    public class YasuoQ3W : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true
        };
        public ObjAIBase _owner;
        Vector2 _start, _end;
        AttackableUnit _target;

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _owner = owner;
            _start = start;
            _end = end;
            _target = target;

            AddBuff("YasoAnimTest", 4f, 1, spell, owner, owner);
        }

        public void OnSpellPostCast(Spell spell)
        {
            _owner.GetSpell("YasuoQ3").Cast(_start, _end, _target);
        }
    }

    public class YasuoQ3 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            FaceDirection(end, owner, true);
        }
        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            AddParticleTarget(owner, owner, "Yasuo_Q_Hand", owner);

            var targetPos = GetPointFromUnit(owner, 1000.0f);
            SpellCast(owner, 3, SpellSlotType.ExtraSlots, targetPos, targetPos, true, Vector2.Zero);

            if (owner.HasBuff("YasuoQ3W"))
            {
                owner.RemoveBuffsWithName("YasuoQ3W");
            }
        }
    }

    public class YasuoQ3Mis : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle,

            },
            IsDamagingSpell = true,
        };
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }
        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            target.TakeDamage(owner, 20f, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false, spell);

            AddBuff("YasuoQ3Mis", 1.2f, 1, spell, target, owner);
            AddParticleTarget(owner, target, "yasuo_base_q_wind_hit_tar.troy", target);
        }
    }
}
