using System.Collections.Generic;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Enums;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Buffs
{
    internal class ColossalStrength : IBuffGameScript
    {
        private ObjAIBase _alistar;
        private Buff _buff;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _alistar = buff.SourceUnit;
            _buff = buff;
            ApiEventManager.OnUpdateStats.AddListener(this, _alistar, OnUpdateStats);
            for (short i = 0; i < 4; i++)
            {
                ApiEventManager.OnSpellPostCast.AddListener(this, _alistar.Spells[i], OnSpellPostCast);
            }
        }

        private void OnUpdateStats(AttackableUnit unit, float diff)
        {
            var baseDmg = 7f + 1f * (_alistar.Stats.Level - 1);
            var ap = _alistar.Stats.AbilityPower.Total * 0.1f;
            var dmg = baseDmg + ap;
            SetBuffToolTipVar(_buff, 0, dmg);
            SetBuffToolTipVar(_buff, 1, baseDmg);
            SetBuffToolTipVar(_buff, 2, ap);
        }

        private void OnSpellPostCast(Spell spell)
        {
            AddBuff("AlistarTrample", 3f, 1, spell, _alistar, _alistar);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            for (short i = 0; i < 4; i++)
            {
                ApiEventManager.OnSpellPostCast.RemoveListener(this, _alistar.Spells[0], OnSpellPostCast);
            }
        }
    }
}