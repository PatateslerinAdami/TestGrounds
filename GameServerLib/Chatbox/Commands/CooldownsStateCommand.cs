using GameServerCore.Enums;
using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.Chatbox;

public class CooldownsStateCommand : ChatCommandBase
{
    public override string Command => "cdstate";
    public override string Syntax => $"{Command} 0 (disable) / 1 (enable)";

    public CooldownsStateCommand(ChatCommandManager chatCommandManager, Game game)
        : base(chatCommandManager, game)
    {
    }

    public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
    {
        var split = arguments.ToLower().Split(' ');
        if (split.Length < 2 || !byte.TryParse(split[1], out var input) || input > 1)
        {
            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR);
            ShowSyntax();
        }
        else
        {
            var enabled = input != 0;
            Game.Config.SetGameFeatures(FeatureFlags.EnableCooldowns, input != 0);
            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO,
    enabled ? "Cooldowns enabled!" : "Cooldowns disabled!");
        }
    }
}