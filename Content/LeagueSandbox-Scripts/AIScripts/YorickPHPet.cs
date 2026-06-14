namespace AIScripts
{
    // Yorick's ghoul/spirit pet — faithful port of Riot's YorickPHPet.lua (4.20). Identical to the
    // autonomous UncontrollablePet brain, only with a longer leash: it follows the owner from up to
    // 2500 (vs 800) and teleports back at 2500 (vs 2000). (FEAR_WANDER_DISTANCE 400 vs 500 is a
    // CrowdControlComponent-global constant — negligible, not parameterised here.)
    public class YorickPHPet : UncontrollablePet
    {
        protected override float ActiveFollowDistance => 2500.0f;
        protected override float TeleportDistance => 2500.0f;
    }
}
