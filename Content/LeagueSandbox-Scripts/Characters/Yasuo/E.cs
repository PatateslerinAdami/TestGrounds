using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class YasuoDashWrapper : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() { };
        
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (target.HasBuff("YasuoDashWrapperChaos"))
            {
                return;
            }
            float lockDuration = 11f - spell.CastInfo.SpellLevel; 
            
            // Aplicăm blocajul pe adversar
            AddBuff("YasuoDashWrapperChaos", lockDuration, 1, spell, target, owner);
            
            // Pornim Dash-ul și animațiile
            AddBuff("YasoAnimTest", 4f, 1, spell, owner, owner);
            AddBuff("YasuoEBlock", 0.5f, 1, spell, target, owner);
        }
    }
}