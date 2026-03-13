using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects;
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
    // in inis i also changed [SpellData] MinimapIconDisplayFlag = 1 to 3, so enemy also could see the missile icon on the minimap.
    public class EnchantedCrystalArrow : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true
        };

        private ObjAIBase _owner;
        public bool isEvolved = false;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }

        public void OnSpellPostCast(Spell spell)
        {
            Vector2 start = new Vector2(spell.CastInfo.SpellCastLaunchPosition.X, spell.CastInfo.SpellCastLaunchPosition.Z);
            Vector2 end = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
            end = start + (end - start).Normalized() * 20000f;

            var parameters = new MissileParameters { Type = MissileType.Arc };
            var m = CreateCustomMissile(_owner, "EnchantedCrystalArrowMissile", start, end, parameters, customHeightOffset:150f);
        }
        public void OnSpellEvolve(Spell spell)
        {
            isEvolved = true;
        }
    }

    public class EnchantedCrystalArrowMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters { Type = MissileType.Arc }
        };

        private ObjAIBase _owner;
        Spell s;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            spell.SpellData.LineWidth = 125f;
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnMissileLaunch, false);
            s = _owner.GetSpell("EnchantedCrystalArrow");
        }

        public void OnMissileLaunch(Spell spell, SpellMissile missile)
        {
            if(s.Script is EnchantedCrystalArrow arrow)
            {
                if(arrow.isEvolved) ApiEventManager.OnCollisionTerrain.AddListener(this, missile, OnTerrainHit, true);
            }
        }

        public void OnTerrainHit(GameObject obj)
        {
            if (!(obj is SpellMissile missile) || missile.IsToRemove()) return;

            Vector2 currentPos = missile.Position;
            Vector2 dir = new Vector2(missile.Direction.X, missile.Direction.Z);

            Vector2 safePos = currentPos - (dir * 20f);

            bool hitXWall = !IsWalkable(safePos.X + (dir.X * 40f), safePos.Y);
            bool hitYWall = !IsWalkable(safePos.X, safePos.Y + (dir.Y * 40f));

            Vector2 newDir = dir;
            if (hitXWall) newDir.X *= -1;
            if (hitYWall) newDir.Y *= -1;
            if (!hitXWall && !hitYWall) newDir *= -1;
            missile.SetToRemove();

            Vector2 newStart = safePos + (newDir * 20f);
            Vector2 newEnd = newStart + (newDir * 20000f);

            CreateCustomMissile(_owner, "EnchantedCrystalArrowMissile", newStart, newEnd, ScriptMetadata.MissileParameters, isOverrideCastPosition:true);//customHeightOffset:200f
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (missile != null)
            {
                target.TakeDamage(_owner, 200f + (50f * spell.CastInfo.SpellLevel), DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
                AddBuff("Stun", 1.5f, 1, spell, target, _owner);

                AddParticle(_owner, target, "ashe_base_r_tar.troy", default);
                missile.SetToRemove();
            }
        }
    }
    public class EnchantedCrystalArrowMissile2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters { Type = MissileType.Target }
        };


        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
            spell.SpellData.LineWidth = 125f;
        }
        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (missile != null)
            {
                var owner = spell.CastInfo.Owner;
                target.TakeDamage(owner, 200f + (50f * spell.CastInfo.SpellLevel), DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
                AddBuff("Stun", 1.5f, 1, spell, target, owner);
                AddParticle(owner, target, "ashe_base_r_tar.troy", default);
                missile.SetToRemove();
            }
        }
    }
}