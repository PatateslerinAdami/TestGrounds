using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class LanternWAlly : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false
        };
        ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var lantern = spell.CastInfo.Targets[0].Unit as Minion;
            var ownerTh = lantern.Owner;
            var spellUser = spell.CastInfo.Owner;
            if (spellUser != ownerTh)
            {
                spellUser.DashToTarget(ownerTh, 700f, keepFacingLastDirection: false, consideredCC: false);
                AddParticle(spellUser, spellUser, "lanterndashsound.troy", default);
                lantern.Die(CreateDeathData(false, 0, lantern, lantern, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
                var wSpell = ownerTh.GetSpell("ThreshWLanternOut");
                if(wSpell.Script is ThreshWLanternOut wOut && wOut.p != null) wOut.p.SetToRemove();
            }
        }
    }

}