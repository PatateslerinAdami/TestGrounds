using GameServerCore.Enums;
using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.Chatbox;

public class ManacostStateCommand : ChatCommandBase
{
    public override string Command => "manacoststate";
    public override string Syntax => $"{Command} 0 (disable) / 1 (enable)";

    public ManacostStateCommand(ChatCommandManager chatCommandManager, Game game)
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
            Game.Config.SetGameFeatures(FeatureFlags.EnableManaCosts, input != 0);
            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO,
    enabled ? "Mana costs enabled!" : "Mana costs disabled!");
        }
    }
}