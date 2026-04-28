using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace GameServerCore.Scripting.CSharp
{
    public interface IOdinCapturePoint
    {
        void AddCapturer(Champion champion);
        void RemoveCapturer(Champion champion);
    }
}