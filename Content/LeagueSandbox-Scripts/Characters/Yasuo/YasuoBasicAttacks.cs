using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class YasuoBasicAttack : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoBasicAttack2 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoBasicAttack3 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoBasicAttack4 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoBasicAttack5 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoBasicAttack6 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoBasicAttack7 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoBasicAttack8 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoBasicAttack9 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }

    public class YasuoCritAttack : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoCritAttack2 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoCritAttack3 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
    public class YasuoCritAttack4 : ISpellScript { public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata(); }
}