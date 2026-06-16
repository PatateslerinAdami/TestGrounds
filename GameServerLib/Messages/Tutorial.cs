namespace LeagueSandbox.GameServer.Messages;

/// <summary>
/// Utility class for sending tutorial-related packets to clients.
/// Used by Lua scripts and game mode logic to display tutorial popups
/// and localized chat text in the 4.20 client.
/// </summary>
public static class Tutorial
{
    /// <summary>
    /// Sends a tutorial popup to a specific client (S2C_OpenTutorialPopup).
    /// Displays a popup box with the given message text.
    /// </summary>
    /// <param name="game">Current game instance.</param>
    /// <param name="clientId">Client to send to.</param>
    /// <param name="message">Message to display in the popup.</param>
    public static void Popup(Game game, int clientId, string message)
    {
        game.PacketNotifier.NotifyS2C_OpenTutorialPopup(clientId, message);
    }

    /// <summary>
    /// Sends localized chat text to a specific client (S2C_DisplayLocalizedTutorialChatText).
    /// Displays a message in the chat box, looked up by localization key.
    /// </summary>
    /// <param name="game">Current game instance.</param>
    /// <param name="clientId">Client to send to.</param>
    /// <param name="message">Localization key or raw message to display in chat.</param>
    public static void Chat(Game game, int clientId, string message)
    {
        game.PacketNotifier.NotifyS2C_DisplayLocalizedTutorialChatText(clientId, message);
    }
}
