namespace LeagueSandbox.GameServer.Content
{
    // Verified 2026-06-07 against the real 4.20 Map1 Constants.var (cfh_* section): Map1
    // DOES include these - our stripped Content/.../Map1/Constants.json just dropped them,
    // so the per-map loader falls back to these defaults. Map1 == Map12 for every field.
    // (TurretRadius corrected to 0.0; the old 1.0 was a transcription error. Both Map1 and
    // Map12 Constants.var say cfh_TurretRadius = 0.000.)
    public class CallForHelpVariables
    {
        /// <summary>
        /// How often a unit will issue a Call For Help
        /// </summary>
        public float Delay { get; set; } = 1.0f;
        /// <summary>
        /// How long a unit should ignore lower prioty calls while the curent target is not activly attacking
        /// </summary>
        public float Stick { get; set; } = 1.5f;
        /// <summary>
        /// Units within this radius will hear your Call For Help
        /// </summary>
        public float Radius { get; set; } = 800.0f;
        /// <summary>
        /// How long a unit will consider a Call For Help.  Mainly used to track whether a unit has already responded.
        /// </summary>
        public float Duration { get; set; } = 1.0f;
        /// <summary>
        /// Attack range buffer distance for melee responders to a Call For Help
        /// </summary>
        public float MeleeRadius { get; set; } = 420.0f;
        /// <summary>
        /// Attack range buffer distance for ranged responders to a Call For Help
        /// </summary>
        public float RangedRadius { get; set; } = 170.0f;
        /// <summary>
        /// Attack range buffer distance for turret responders to a Call For Help
        /// </summary>
        public float TurretRadius { get; set; } = 0.0f;
    }
}
