
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
    public class YasuoQW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };

        public int QStack = 0;
        public ObjAIBase _owner;
        Vector2 _start, _end;
        AttackableUnit _target;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            if (spell.CastInfo.SpellLevel == 0) ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        }
        private void OnLevelUpSpell(Spell spell)
        {
            SetSpell(_owner, "YasuoQ", SpellSlotType.ExtraSlots, 0, true);
            SetSpell(_owner, "YasuoQ2", SpellSlotType.ExtraSlots, 1, true);
            SetSpell(_owner, "YasuoQ3", SpellSlotType.ExtraSlots, 2, true);
            SetSpell(_owner, "YasuoQ3Mis", SpellSlotType.ExtraSlots, 3, true);

            SetSpell(_owner, "YasuoWMovingWallMissile", SpellSlotType.ExtraSlots, 4, true);
            ApiEventManager.OnLevelUpSpell.RemoveListener(this, spell);
        }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _start = start;
            _end = end;
            _target = target;

            AddBuff("YasoAnimTest", 4f, 1, spell, owner, owner);
        }

        public void OnSpellPostCast(Spell spell)
        {
            _owner.GetSpell("YasuoQ").Cast(_start, _end, _target);
        }
    }

    public class YasuoQ : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };
        bool FirstTarget = true;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            FaceDirection(end, owner, true);
            AddParticleTarget(owner, owner, "yasuo_base_q_windstrike.troy", owner, size: 0.9f);
            FirstTarget = true;
        }
        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            AddParticleTarget(owner, owner, "Yasuo_Q_Hand", owner);
            spell.CreateSpellSector(new SectorParameters
            {
                BindObject = owner,
                Length = 450f,
                Width = 100f,
                PolygonVertices = new Vector2[]
                {
                    new Vector2(-1, 0),
                    new Vector2(-1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 0)
                },
                SingleTick = true,
                Type = SectorType.Polygon
            });
        }
        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            var damage = owner.Stats.AttackDamage.Total * (0.45f + spell.CastInfo.SpellLevel * 0.15f) + (50 + spell.CastInfo.SpellLevel * 30);
            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
            if (FirstTarget)
            {
                AddParticleTarget(owner, target, "Yasuo_Base_Q_hit_tar", target);
                FirstTarget = false;
                AddParticleTarget(owner, owner, "yasuo_q2_ready_buff.troy", owner, size: 1f);
                AddBuff("YasuoQ", 3f, 1, spell, owner, owner);
            }
        }

    }
}
