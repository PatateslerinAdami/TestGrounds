using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class ItemLifelineCooldown : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(
            LeagueSandbox.GameServer.GameObjects.AttackableUnits.AttackableUnit unit,
            LeagueSandbox.GameServer.GameObjects.Buff buff,
            LeagueSandbox.GameServer.GameObjects.SpellNS.Spell spell
        )
        {
            if (buff.Name != "ItemLifelineCooldown" && buff.Name != "ItemSpellbladeCooldown")
            {
                return;
            }

            buff.Variables.Set("presentationPersistsThroughDeath", true);
            ItemHelpers.NativeItemPresentation.Show(buff, buff.Name == "ItemLifelineCooldown" ? "HexdrinkerTimerCD" : "SheenDelay");
        }

        public void OnDeactivate(
            LeagueSandbox.GameServer.GameObjects.AttackableUnits.AttackableUnit unit,
            LeagueSandbox.GameServer.GameObjects.Buff buff,
            LeagueSandbox.GameServer.GameObjects.SpellNS.Spell spell
        )
        {
            ItemHelpers.NativeItemPresentation.Remove(buff);
        }
    }

    public class HasBeenRevived : ItemLifelineCooldown
    {
    }
}
