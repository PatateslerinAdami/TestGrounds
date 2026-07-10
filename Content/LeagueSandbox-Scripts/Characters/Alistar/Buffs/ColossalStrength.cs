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
            for (short i = 0; i < 4; i++)
            {
                ApiEventManager.OnSpellPostCast.AddListener(this, _alistar.Spells[i], OnSpellPostCast);
            }
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