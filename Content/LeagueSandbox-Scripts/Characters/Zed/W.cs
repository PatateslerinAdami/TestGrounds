using System;
using System.Numerics;
using System.Collections.Generic;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class ZedShadowDash : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() { TriggersSpellCasts = false };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var targetPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            var maxRange = spell.GetCurrentCastRange();

            if (Vector2.Distance(owner.Position, targetPos) > maxRange)
            {
                targetPos = owner.Position + (Vector2.Normalize(targetPos - owner.Position) * maxRange);
            }

            if (!IsWalkable(targetPos.X, targetPos.Y))
            {
                targetPos = GetClosestTerrainExit(targetPos);
            }

            CreateCustomMissile(owner, "ZedShadowDashMissile", owner.Position, targetPos, new MissileParameters { Type = MissileType.Circle });
            SealSpellSlot(owner, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
            owner.PlayAnimation("Spell2_Cast", timeScale: 0.7f);
        }
    }

    public class ZedShadowDashMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            MissileParameters = new MissileParameters { Type = MissileType.Circle }
        };

        private ObjAIBase _owner;
        private Vector2 _shadowDestination;

        public Minion shadow;
        public bool MissileInFlight { get; private set; }
        public List<Action> PendingShadowCasts { get; } = new List<Action>();

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunch, false);

            shadow = AddMinion(owner, "ZedShadow", "ZedShadow", default, owner.Team, owner.SkinID, true, false, false, default, default, true, true, true);
            shadow.SetStatus(StatusFlags.NoRender, true);

            //spell.SpellData.MissileSpeed = 2500;
        }

        public void OnLaunch(Spell spell, SpellMissile missile)
        {
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);

            _shadowDestination = new Vector2(missile.CastInfo.TargetPositionEnd.X, missile.CastInfo.TargetPositionEnd.Z);
            MissileInFlight = true;
        }

        public void OnMissileEnd(SpellMissile missile)
        {
            MissileInFlight = false;
            var owner = missile.CastInfo.Owner;

            shadow.TeleportTo(_shadowDestination.X, _shadowDestination.Y);
            shadow.PlayAnimation("Idle1", timeScale: 0.8f);

            foreach (var action in PendingShadowCasts)
            {
                action();
            }
            PendingShadowCasts.Clear();

            //AddBuff(new ZedWHandler { shadow = shadow }, "ZedWHandler", 4f, 1, missile.SpellOrigin, owner, owner);
            //AddBuff(new ZedWHandler2 { shadow = shadow }, "ZedWHandler2", 4f, 1, missile.SpellOrigin, owner, owner);

            AddParticle(_owner, null, "Zed_Base_W_tar.troy", missile.Position, lifetime: 1f);
        }
    }

    public class ZedW2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() { TriggersSpellCasts = true };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var buff = owner.GetBuffWithName("ZedWHandler");

            if (buff?.BuffScript is ZedWHandler handler && handler.shadow != null)
            {
                var zedPos = owner.Position;
                var shadowPos = handler.shadow.Position;

                TeleportTo(owner, shadowPos.X, shadowPos.Y);
                TeleportTo(handler.shadow, zedPos.X, zedPos.Y);

                AddParticle(owner, null, "zed_base_cloneswap.troy", shadowPos, lifetime: 1f);
                AddParticle(owner, null, "zed_base_cloneswap.troy", zedPos, lifetime: 1f);
            }

            owner.RemoveBuffsWithName("ZedWHandler");
        }
    }
}