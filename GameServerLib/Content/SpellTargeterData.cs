namespace LeagueSandbox.GameServer.Content
{
    /// <summary>
    /// One entry of a spell's <c>SpellTargeter{N}</c> JSON block — the client-side cast
    /// indicator (range circle, line, cone, minimap marker, ...). Riot ships up to ~3 of
    /// these per spell, indexed 1..N in JSON; we expose them as a zero-indexed array.
    ///
    /// <para>For charge spells the <see cref="RangeGrowthDuration"/> and
    /// <see cref="RangeGrowthMax"/> fields drive the client's charge-bar visual range
    /// growth. These are intentionally distinct from <c>SpellData.CastRangeGrowthDuration</c>
    /// / <c>CastRangeGrowthMax</c> which drive the actual cast-side range — Riot routinely
    /// uses slightly different values (Varus Q: cast grows 925→1600 over 1.3s but targeter
    /// grows up to 1680 over 1.5s).</para>
    /// </summary>
    public class SpellTargeterData
    {
        /// <summary>Visual style: "Range", "Line", "MinimapRange", "Cone", ...</summary>
        public string DrawableType { get; set; } = "";

        public bool AlwaysDraw { get; set; }
        public bool HasMaxGrowRangeTexture { get; set; }
        public bool HideWithLineIndicator { get; set; }
        public bool LineStopsAtEndPosition { get; set; }
        public bool UseGlobalLineIndicator { get; set; }

        /// <summary>Targeter base range when not derived from <c>SpellData.CastRange</c>.</summary>
        public float OverrideBaseRange { get; set; }

        public float LineWidth { get; set; }

        /// <summary>Charge-bar visual growth time. Distinct from <c>SpellData.CastRangeGrowthDuration</c>.</summary>
        public float RangeGrowthDuration { get; set; }

        /// <summary>Maximum visual range at full charge. Distinct from <c>SpellData.CastRangeGrowthMax</c>.</summary>
        public float RangeGrowthMax { get; set; }

        /// <summary>e.g. "Owner", "OwnerStartPos" — where the targeter origin anchors.</summary>
        public string CenterBasePosition { get; set; } = "";
        public string StartBasePosition { get; set; } = "";
        public string StartOrientationType { get; set; } = "";
        public string EndBasePosition { get; set; } = "";
        public string EndOrientationType { get; set; } = "";

        public string TextureMaxGrow { get; set; } = "";
        public string TextureBaseMaxGrow { get; set; } = "";
        public string TextureTargetMaxGrow { get; set; } = "";
        public string TextureBaseOverride { get; set; } = "";
        public string TextureTargetOverride { get; set; } = "";

        public void Load(ContentFile file, string section)
        {
            DrawableType = file.GetString(section, "DrawableType", DrawableType);
            AlwaysDraw = file.GetBool(section, "AlwaysDraw", AlwaysDraw);
            HasMaxGrowRangeTexture = file.GetBool(section, "HasMaxGrowRangeTexture", HasMaxGrowRangeTexture);
            HideWithLineIndicator = file.GetBool(section, "HideWithLineIndicator", HideWithLineIndicator);
            LineStopsAtEndPosition = file.GetBool(section, "LineStopsAtEndPosition", LineStopsAtEndPosition);
            UseGlobalLineIndicator = file.GetBool(section, "UseGlobalLineIndicator", UseGlobalLineIndicator);
            OverrideBaseRange = file.GetFloat(section, "OverrideBaseRange", OverrideBaseRange);
            LineWidth = file.GetFloat(section, "LineWidth", LineWidth);
            RangeGrowthDuration = file.GetFloat(section, "RangeGrowthDuration", RangeGrowthDuration);
            RangeGrowthMax = file.GetFloat(section, "RangeGrowthMax", RangeGrowthMax);
            CenterBasePosition = file.GetString(section, "Center_BasePosition", CenterBasePosition);
            StartBasePosition = file.GetString(section, "Start_BasePosition", StartBasePosition);
            StartOrientationType = file.GetString(section, "Start_OrientationType", StartOrientationType);
            EndBasePosition = file.GetString(section, "End_BasePosition", EndBasePosition);
            EndOrientationType = file.GetString(section, "End_OrientationType", EndOrientationType);
            TextureMaxGrow = file.GetString(section, "TextureMaxGrow", TextureMaxGrow);
            TextureBaseMaxGrow = file.GetString(section, "TextureBaseMaxGrow", TextureBaseMaxGrow);
            TextureTargetMaxGrow = file.GetString(section, "TextureTargetMaxGrow", TextureTargetMaxGrow);
            TextureBaseOverride = file.GetString(section, "TextureBaseOverride", TextureBaseOverride);
            TextureTargetOverride = file.GetString(section, "TextureTargetOverride", TextureTargetOverride);
        }
    }
}
