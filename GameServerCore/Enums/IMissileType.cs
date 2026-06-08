namespace GameServerCore.Enums
{
    public enum MissileType : int
    {
        /// <summary>
        /// Unused. If a MissileGameScript is implemented which controls flight path, use this (possibly rename to "Custom").
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Single target missile.
        /// </summary>
        Target = 0x1,
        /// <summary>
        /// Single target missile which can change target after reaching its current target.
        /// </summary>
        Chained = 0x2,
        /// <summary>
        /// Location-targeted missile that travels straight horizontally toward the target
        /// while arcing vertically (ballistic lob via gravity / height augment).
        /// Backed by SpellLineMissile. Mirrors client CastType ArcMissile (3).
        /// </summary>
        Arc = 0x3,
        /// <summary>
        /// Location-targeted missile. In the client this is ALWAYS polar/orbit motion around a
        /// center (or a tracked target) via CircleMissileAngularVelocity/RadialVelocity — e.g.
        /// Diana W orbs (S1 obj_SpellCircleMissile::UpdateProjectile: pos = center + cos/sin*radius,
        /// unconditional; with both velocities 0 it sits at a fixed offset / attaches to the center.
        /// S4 confirms the structure — mRotateRadius/mRotatePhase, polar MISREP direction params —
        /// but its UpdateCircleMissile body is still a decomp stub). cSpellCircleMissile
        /// mirrors this faithfully since 2026-06-07: unconditional polar motion, zero
        /// velocities = fixed offset / attachment (the old straight-line fallback was removed —
        /// straight missiles belong on Arc/SpellLineMissile). Mirrors client CastType
        /// CircleMissile (4).
        /// </summary>
        Circle = 0x4,
    }
}