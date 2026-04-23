namespace GameServerLib.GameObjects
{
    public class Fade
    {
        internal readonly int Id;
        internal readonly float StartTime;
        internal readonly float Duration;
        internal readonly float Opacity;

        internal Fade(int id, float startTime, float duration, float opacity)
        {
            Id = id;
            StartTime = startTime;
            Duration = duration;
            Opacity = opacity;
        }
    }
}
