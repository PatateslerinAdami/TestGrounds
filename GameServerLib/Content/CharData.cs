using System;
using System.Collections.Generic;
using System.Linq;
using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.Content
{
    public class PassiveData
    {
        public string PassiveAbilityName { get; set; } = "";
        public int[] PassiveLevels { get; set; } = new int[6];
        public string PassiveLuaName { get; set; } = "";
        public string PassiveNameStr { get; set; } = "";

        // Single passive is sufficient: patch 4.20 stores exactly one passive per champion.
        // Verified by hashing every char-stat .json key (ihash "Data*Passive{i}{field}", 65599 algo)
        // against the UNKNOWN_HASHES sections of all 173 champions — no Passive2..6 field (Name/
        // LuaName/AbilityName/Desc/Range) exists in the 4.20 data. Riot's S1 engine loop-parsed a
        // Passives[] array (bipedmodel.cpp), but only index 1 is ever populated in this era.
    }

    public class CharData
    {
        // Riot engine default: CharacterData.cpp:907 ReadCFG_F(..., "AcquisitionRange", 750.0f).
        // Load-bearing for exactly the units whose 4.20 inibin omits the key — lane MELEE and
        // SIEGE minions (ranged=700, super/mech=600, champions=600 are all explicit in data).
        // Riot's acquisition ordering is melee 750 > caster 700 > super 600; the old 475 default
        // inverted it and made melees march ~275u past the enemy front before locking anything
        // (F2, docs/PATHING_AUDIT_2026_07_19.md).
        public float AcquisitionRange { get; private set; } = 750;
        // First Wave Special Rules -> The Wake-Up Range is smaller than the normal
        // Acquisition Range. While a First Wave Minion is asleep, it only checks this radius
        // before waking up. The FirstAcquisitionRange is larger and is used exactly once immediately
        // after waking up to obtain a wider selection of targets.
        public float WakeUpRange { get; private set; } = 600;
        public float FirstAcquisitionRange { get; private set; } = 1100;
        public bool AllyCanUse { get; private set; } = false;
        public bool AlwaysVisible { get; private set; } = false;
        public bool AlwaysUpdatePAR { get; private set; } = false;
        public float Armor { get; private set; } = 1.0f;
        public float ArmorPerLevel { get; private set; } = 1.0f;
        public float AttackCastTime { get; private set; } = 0.0f;
        public float AttackDelayCastOffsetPercent { get; private set; } = 0.0f;
        // Riot default = 1.0 (NOT 0): the auto-attack WINDUP fully scales with attack speed. This is the
        // ratio in Spell::ComputeCharacterAttackCastDelay's Lerp(unscaledWindup, AS-scaledWindup, ratio):
        // 1 = windup shrinks with AS like everyone expects; <1 keeps it partly unscaled (Kalista 0.5,
        // Thresh 0.25 — they specify it). Standard ADCs (Caitlyn/Jinx/Tristana/Ezreal) OMIT the field and
        // rely on this default. With the old 0.0 default their server windup stayed at the slow base value
        // while the client scaled it down → at high attack speed the server launched the missile/dealt the
        // hit visibly late (desync vs the client's missile). Champions that genuinely want an unscaled
        // windup set 0 explicitly.
        public float AttackDelayCastOffsetPercentAttackSpeedRatio { get; private set; } = 1.0f;
        public float AttackDelayOffsetPercent { get; private set; } = 0.0f;
        public float AttackRange { get; private set; } = 100.0f;
        public float AttackSpeedPerLevel { get; private set; }
        public float AttackTotalTime { get; private set; } = 0.0f;
        /// <summary>
        // REMOVED: ChasingAttackRangePercent. The JSON field exists in stats files (even back to
        // the S1 .ini data), but it has ZERO code consumers in every corpus (S4 mac decomp field
        // reads are all literal ReadCFG strings — none match; Ghidra S4, herowars S1 server and
        // both Lua corpora: 0 hits; SDBM hash not present as an immediate) → vestigial data. An
        // inferred wiring into the auto-attack engage range (2026-06-21) was FALSIFIED in-game
        // (2026-07-15): Master Yi's 0.3 shrank the engage gate below a turret's navgrid footprint
        // → walked up and never swung. See memory project_chardata_chasing_postattack_loaded.
        /// <summary>
        /// JSON field present in many character stats files but VERIFIED 2026-05-10 to be
        /// unread by the S4 client (same verification trail as the removed ChasingAttackRangePercent
        /// — see the note above). Loaded for forward-compat, NOT wired into gameplay.
        /// </summary>
        public float PostAttackMoveDelay { get; private set; } = 0.0f;
        // S4 default 1.0 (CharacterData.cpp:966) the slot 0 catches the whole roll unless
        // the data explicitly lowers it (alternating champs use 0.5). Keep 1.0 (current
        // branch's verified value) over experimental's stale 0.5.
        public float BaseAttackProbability { get; private set; } = 1.0f;
        public float BaseDamage { get; private set; } = 10.0f;
        public float BaseHp { get; private set; } = 100.0f;
        public float BaseMp { get; private set; } = 100.0f;
        // HP regen scaling with CURRENT max HP: regen = BaseStaticHPRegen + BaseFactorHPRegen * MaxHP.
        // Wire-verified on AnnieTibbers (static 0.5, factor 0.0015): mHPRegenRate replicates 2.3 at
        // 1200 max HP and 5.0 after the R3 buff raises max HP to 3000 — so the factor tracks the
        // buffed total, not the char-data base. Nonzero only on pets/monsters/props (38 units).
        public float BaseFactorHpRegen { get; private set; } = 0.0f;
        public float BaseStaticHpRegen { get; private set; } = 0.30000001f;
        public float BaseStaticMpRegen { get; private set; } = 0.30000001f;
        public float CooldownSpellSlot { get; private set; } = 0.0f;
        public float CritDamageBonus { get; private set; } = 2.0f;
        public float CritAttackDelayCastOffsetPercent { get; private set; } = 0.0f;
        // Riot default = 1.0 (crit-attack windup scales with attack speed too); same reasoning as the
        // non-crit AttackDelayCastOffsetPercentAttackSpeedRatio above.
        public float CritAttackDelayCastOffsetPercentAttackSpeedRatio { get; private set; } = 1.0f;
        public float CritAttackDelayOffsetPercent { get; private set; } = 0.0f;
        // S4 default 2.0 (CharacterData.cpp:1008 loads every non-base slot incl. crit
        // with the 2.0 catch-all default). The crit walk's first slot always catches.
        public float CritAttackProbability { get; private set; } = 2.0f;
        public float DamagePerLevel { get; private set; } = 10.0f;
        public bool DisableContinuousTargetFacing { get; private set; } = false;
        public bool EnemyCanUse { get; private set; } = false;
        public float ExpGivenOnDeath { get; private set; } = 0.0f;
        public float GameplayCollisionRadius { get; private set; } = 65.0f;
        public float GlobalExpGivenOnDeath { get; private set; } = 0.0f;
        public float GlobalGoldGivenOnDeath { get; private set; } = 0.0f;
        public float GoldGivenOnDeath { get; private set; } = 0.0f;
        public string HeroUseSpell { get; private set; } = string.Empty;
        public float HpPerLevel { get; private set; } = 10.0f;
        public float HpRegenPerLevel { get; private set; }
        public bool IsMelee { get; private set; } //Yes or no
        public bool Immobile { get; private set; } = false;
        /// <summary>
        /// CharData "NeverRender" (invisible dummy units like TestCubeRender*): the S4 client maps
        /// it to CharacterRecordFlags::kNeverRender and sets CharState.SetNoRender(true) at load
        /// (AIBase.cpp:500) — IsVisible() then returns false, hiding model AND health bar (particles
        /// only render with ForceRenderParticles). The state is REPLICATED, so the server must set
        /// it too (StatusFlags.NoRender at unit init) or the first replication clobbers the client's
        /// locally-derived bit and the health bar pops back in.
        /// </summary>
        public bool NeverRender { get; private set; } = false;
        public bool IsTower { get; private set; } = false;
        public bool IsUseable { get; private set; } = false;
        /// <summary>
        /// Useable "GoldRedirectTargetUseableOnly" (UseableComponent.cpp:57): when set, ONLY the
        /// unit's GoldRedirectTarget may use the object — overrides the Ally/Enemy/Minion gates.
        /// </summary>
        public bool GoldRedirectTargetUseableOnly { get; private set; } = false;
        public float LocalGoldGivenOnDeath { get; private set; } = 0.0f;
        // Riot's raw ini key is "MinionUsable" (no second 'e'), but our inibin converter
        // normalized it to "MinionUseable" in the JSON exports (verified: 5 Stats JSONs carry
        // "MinionUseable", none "MinionUsable") — so the parse key below matches OUR data.
        // Default TRUE per UseableComponent's ctor + InitFromFile.
        public bool MinionUseable { get; private set; } = true;
        public string MinionUseSpell { get; private set; } = string.Empty;
        public int MoveSpeed { get; private set; } = 100;
        public float MpPerLevel { get; private set; } = 10.0f;
        public float MpRegenPerLevel { get; private set; }
        public PrimaryAbilityResourceType ParType { get; private set; } = PrimaryAbilityResourceType.Mana;
        /// <summary>
        /// Chardata "PARDisplayThroughDeath" — in the whole 4.20 content set ONLY Shyvana(Dragon)
        /// carries it, and she is exactly the champion whose PAR survives death on the wire
        /// (HeroReincarnateAlive echoes her CURRENT fury instead of a reset value). Nominally an
        /// HUD flag (keep drawing the PAR bar while dead), which only works if the value persists —
        /// so it doubles as the data-driven "keep PAR through death" marker used by
        /// Champion.Respawn.
        /// </summary>
        public bool PARDisplayThroughDeath { get; private set; } = false;
        public float PathfindingCollisionRadius { get; private set; } = -1.0f;
        /// <summary>
        /// Riot resolves the default at LOAD time (CharacterData.cpp:1049): per-char inibin value,
        /// falling back to GamePermanent.cfg [Vision] PerceptionBubbleRadius — which is 1350 in the
        /// 4.20 client (and 1350 is also the code fallback if the cfg key were absent). 355/410 of
        /// our chardata files (incl. every champion) omit the key, so this default carries them.
        /// (Previously 0, which made ObjAIBase invent an 1100 fallback — no Riot source for either.)
        /// </summary>
        public float PerceptionBubbleRadius { get; private set; } = 1350.0f;
        /// <summary>
        /// Riot "SelectionRadius" (CharacterData.cpp:1175 loads it as overrideCollisionRadius,
        /// default -1). Consumed by obj_AI_Base::ReinitializeUnitRadius: SelectionRadius = this if
        /// &gt; 0 else GetBoundingRadius(), then a missing PathfindingCollisionRadius (-1) resolves
        /// to 0.5 × SelectionRadius. Needed for the faithful pathfinding-radius fallback.
        /// </summary>
        public float SelectionRadius { get; private set; } = -1.0f;
        public bool ShouldFaceTarget { get; private set; } = true;
        public float SpellBlock { get; private set; }
        public float SpellBlockPerLevel { get; private set; }
        public UnitTag UnitTags { get; private set; }

        public string[] SpellNames { get; private set; } = new string[4];
        public string[] ExtraSpells { get; private set; } = new string[16];

        // Form-changer (Elise/Jayce/Nidalee/Shyvana/Udyr) alternate-form data. The inibin keys are a
        // 4.20-era addition absent from every hash→name dictionary, so they have no recoverable field
        // name and are read directly by their raw inibin key hash (see ContentFile.GetStringByHash).
        // AlternateFormSpells = the alternate form's Q/W/E/R; empty for stance/reuse forms (Shyvana,
        // Udyr) that don't carry a 4-slot alternate spellbook. AlternateCharacterName = the alternate
        // form's character/model (EliseSpider, JayceCannon, Nidalee_Cougar, ShyvanaDragon, UdyrPhoenix).
        public string[] AlternateFormSpells { get; private set; } = new string[4];
        public string AlternateCharacterName { get; private set; } = "";
        public int[] MaxLevels { get; private set; } = { 5, 5, 5, 3 };

        public int[][] SpellsUpLevels { get; private set; } =
        {
            new[] {1, 3, 5, 7, 9, 99},
            new[] {1, 3, 5, 7, 9, 99},
            new[] {1, 3, 5, 7, 9, 99},
            new[] {6, 11, 16, 99, 99, 99}
        };

        public List<BasicAttackInfo> BasicAttacks { get; private set; } = Enumerable.Repeat(new BasicAttackInfo(), 18).ToList();

        // TODO: Verify if we want this to be an array.
        public PassiveData PassiveData { get; private set; } = new PassiveData();

        public CharData Load(ContentFile file)
        {
            string name = file.Name;

            AcquisitionRange = file.GetFloat("Data", "AcquisitionRange", AcquisitionRange);
            //Does not exist
            WakeUpRange = file.GetFloat("Data", "WakeUpRange", WakeUpRange);
            FirstAcquisitionRange = file.GetFloat("Data", "FirstAcquisitionRange", FirstAcquisitionRange);
            //
            Armor = file.GetFloat("Data", "Armor", Armor);
            ArmorPerLevel = file.GetFloat("Data", "ArmorPerLevel", ArmorPerLevel);

            AttackCastTime = file.GetFloat("Data", "AttackCastTime", AttackCastTime);
            AttackDelayCastOffsetPercent = file.GetFloat("Data", "AttackDelayCastOffsetPercent", AttackDelayCastOffsetPercent);
            AttackDelayCastOffsetPercentAttackSpeedRatio = file.GetFloat("Data", "AttackDelayCastOffsetPercentAttackSpeedRatio", AttackDelayCastOffsetPercentAttackSpeedRatio);
            AttackDelayOffsetPercent = file.GetFloat("Data", "AttackDelayOffsetPercent", AttackDelayOffsetPercent);
            AttackRange = file.GetFloat("Data", "AttackRange", AttackRange);
            AttackSpeedPerLevel = file.GetFloat("Data", "AttackSpeedPerLevel", AttackSpeedPerLevel);
            AttackTotalTime = file.GetFloat("Data", "AttackTotalTime", AttackTotalTime);
            PostAttackMoveDelay = file.GetFloat("Data", "PostAttackMoveDelay", PostAttackMoveDelay);

            BaseAttackProbability = file.GetFloat("Data", "BaseAttack_Probability", BaseAttackProbability);
            BaseDamage = file.GetFloat("Data", "BaseDamage", BaseDamage);
            BaseHp = file.GetFloat("Data", "BaseHP", BaseHp);
            BaseMp = file.GetFloat("Data", "BaseMP", BaseMp);
            BaseFactorHpRegen = file.GetFloat("Data", "BaseFactorHPRegen", BaseFactorHpRegen);
            BaseStaticHpRegen = file.GetFloat("Data", "BaseStaticHPRegen", BaseStaticHpRegen);
            BaseStaticMpRegen = file.GetFloat("Data", "BaseStaticMPRegen", BaseStaticMpRegen);

            CritAttackDelayCastOffsetPercent = file.GetFloat("Data", "CritAttack_AttackDelayCastOffsetPercent", CritAttackDelayCastOffsetPercent);
            CritAttackDelayCastOffsetPercentAttackSpeedRatio = file.GetFloat("Data", "CritAttack_AttackDelayCastOffsetPercentAttackSpeedRatio", CritAttackDelayCastOffsetPercentAttackSpeedRatio);
            CritAttackDelayOffsetPercent = file.GetFloat("Data", "CritAttack_AttackDelayOffsetPercent", CritAttackDelayOffsetPercent);
            CritDamageBonus = file.GetFloat("Data", "CritDamageBonus", CritDamageBonus);

            DamagePerLevel = file.GetFloat("Data", "DamagePerLevel", DamagePerLevel);
            DisableContinuousTargetFacing = file.GetBool("Data", "DisableContinuousTargetFacing");
            ExpGivenOnDeath = file.GetFloat("Data", "ExpGivenOnDeath", ExpGivenOnDeath);
            GameplayCollisionRadius = file.GetFloat("Data", "GameplayCollisionRadius", GameplayCollisionRadius);
            GlobalExpGivenOnDeath = file.GetFloat("Data", "GlobalExpGivenOnDeath", GlobalExpGivenOnDeath);
            GlobalGoldGivenOnDeath = file.GetFloat("Data", "GlobalGoldGivenOnDeath", GlobalGoldGivenOnDeath);
            GoldGivenOnDeath = file.GetFloat("Data", "GoldGivenOnDeath", GoldGivenOnDeath);
            HpRegenPerLevel = file.GetFloat("Data", "HPRegenPerLevel", HpRegenPerLevel);
            HpPerLevel = file.GetFloat("Data", "HPPerLevel", HpPerLevel);
            Immobile = file.GetBool("Data", "Imobile", Immobile);

            var isMeleeStr = file.GetString("Data", "IsMelee", IsMelee ? "Yes" : "No");
            IsMelee = isMeleeStr.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                   || isMeleeStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            LocalGoldGivenOnDeath = file.GetFloat("Data", "LocalGoldGivenOnDeath", LocalGoldGivenOnDeath);
            MoveSpeed = file.GetInt("Data", "MoveSpeed", MoveSpeed);
            NeverRender = file.GetBool("Data", "NeverRender", NeverRender);
            MpRegenPerLevel = file.GetFloat("Data", "MPRegenPerLevel", MpRegenPerLevel);
            MpPerLevel = file.GetFloat("Data", "MPPerLevel", MpPerLevel);
            PathfindingCollisionRadius = file.GetFloat("Data", "PathfindingCollisionRadius", PathfindingCollisionRadius);
            PerceptionBubbleRadius = file.GetFloat("Data", "PerceptionBubbleRadius", PerceptionBubbleRadius);
            SelectionRadius = file.GetFloat("Data", "SelectionRadius", SelectionRadius);
            ShouldFaceTarget = file.GetBool("Data", "ShouldFaceTarget", ShouldFaceTarget);
            SpellBlock = file.GetFloat("Data", "SpellBlock", SpellBlock);
            SpellBlockPerLevel = file.GetFloat("Data", "SpellBlockPerLevel", SpellBlockPerLevel);

            EnemyCanUse = file.GetBool("Useable", "EnemyCanUse", EnemyCanUse);
            AllyCanUse = file.GetBool("Useable", "AllyCanUse", AllyCanUse);
            HeroUseSpell = file.GetString("Useable", "HeroUseSpell", HeroUseSpell);
            CooldownSpellSlot = file.GetFloat("Useable", "CooldownSpellSlot", CooldownSpellSlot);
            IsUseable = file.GetBool("Useable", "IsUseable", IsUseable);
            GoldRedirectTargetUseableOnly = file.GetBool("Useable", "GoldRedirectTargetUseableOnly", GoldRedirectTargetUseableOnly);
            MinionUseable = file.GetBool("Useable", "MinionUseable", MinionUseable);
            MinionUseSpell = file.GetString("Useable", "MinionUseSpell", MinionUseSpell);

            AlwaysVisible = file.GetBool("Minion", "AlwaysVisible", AlwaysVisible);
            IsTower = file.GetBool("Minion", "IsTower", IsTower);
            AlwaysUpdatePAR = file.GetBool("Minion", "AlwaysUpdatePAR", AlwaysUpdatePAR);

            foreach (var tag in file.GetString("Data", "UnitTags").Split(" | "))
            {
                Enum.TryParse(tag, out UnitTag unitTag);
                UnitTags |= unitTag;
            }

            PARDisplayThroughDeath = file.GetBool("Data", "PARDisplayThroughDeath", PARDisplayThroughDeath);
            Enum.TryParse<PrimaryAbilityResourceType>(file.GetString("Data", "PARType", ParType.ToString()),
                out var tempPar);
            ParType = tempPar;

            for (var i = 0; i < 4; i++)
            {
                SpellNames[i] = file.GetString("Data", $"Spell{i + 1}", "");
            }

            for (var i = 0; i < 16; i++)
            {
                ExtraSpells[i] = file.GetString("Data", $"ExtraSpell{i + 1}", "");
            }

            // Alternate-form spells live under four consecutive inibin key hashes (2074622752..2074622755
            // = alt-form Spell1..4); the alternate character/model is hash 3309724546. These have no
            // known field name (see AlternateFormSpells declaration), so they are read by raw hash.
            for (var i = 0; i < 4; i++)
            {
                AlternateFormSpells[i] = file.GetStringByHash(2074622752u + (uint)i, "");
            }
            AlternateCharacterName = file.GetStringByHash(3309724546u, "");

            for (var i = 0; i < 4; i++)
            {
                var defaultLevels = SpellsUpLevels[i];
                var levels = file.GetIntArray("Data", $"SpellSupLevels{i + 1}", defaultLevels);
                SpellsUpLevels[i] = file.GetIntArray("Data", $"SpellsUpLevels{i + 1}", levels);
            }

            PassiveData.PassiveLuaName = file.GetString("Data", "Passive1LuaName", "");

            // PassiveLevels[i] = the champion level at which passive rank i+1 unlocks
            // (Passive1Level1..6 in the data, e.g. 1/4/7/10/13/16). Absent keys stay 0.
            for (var i = 0; i < PassiveData.PassiveLevels.Length; i++)
            {
                PassiveData.PassiveLevels[i] = file.GetInt("Data", $"Passive1Level{i + 1}", 0);
            }

            MaxLevels = file.GetIntArray("Data", "MaxLevels", MaxLevels);

            //Main AutoAttack
            BasicAttacks[0] = new BasicAttackInfo(AttackDelayOffsetPercent, AttackDelayCastOffsetPercent, AttackDelayCastOffsetPercentAttackSpeedRatio)
            {
                Name = name + "BasicAttack",
                AttackCastTime = AttackCastTime,
                AttackTotalTime = AttackTotalTime,
                Probability = BaseAttackProbability
            };
            BasicAttacks[0].GetAttackValues();

            int nameIndex = 2;
            //Secondary/Extra AutoAttacks
            for (var i = 1; i < 9; i++)
            {
                var attackName = file.GetString("Data", $"ExtraAttack{i}", "");

                //AncientGolem for example, doesn't have his ExtraAttacks explicitly defined in his file, but it has "ExtraAttack_Probability" which implies the existance of ExtraAttacks
                if (string.IsNullOrEmpty(attackName) && file.HasMentionOf("Data", $"ExtraAttack{i}"))
                {
                    attackName = $"{name}BasicAttack{nameIndex}";
                }

                if (BasicAttacks.Find(x => x.Name == attackName) != null)
                {
                    nameIndex++;
                    continue;
                }
                float offsetPercent = AttackDelayCastOffsetPercent = file.GetFloat("Data", $"ExtraAttack{i}_AttackDelayCastOffsetPercent", AttackDelayCastOffsetPercent);

                BasicAttacks[i] = new BasicAttackInfo(AttackDelayOffsetPercent, offsetPercent, AttackDelayCastOffsetPercentAttackSpeedRatio)
                {
                    Name = attackName,
                    AttackCastTime = file.GetFloat("Data", $"ExtraAttack{i}_AttackCastTime", AttackCastTime),
                    AttackTotalTime = file.GetFloat("Data", $"ExtraAttack{i}_AttackTotalTime", AttackTotalTime),
                    // S4 default 2.0 (CharacterData.cpp:1008): a slot without explicit
                    // probability is a CATCH-ALL. It takes the whole remaining roll IF
                    // the cumulative walk ever reaches it. Slots behind cumsum >= 1.0 are
                    // unreachable; that is how Riot's data keeps conditional attacks
                    // (form attacks, passive procs, fossils like SivirBasicAttack3/4)
                    // out of the rotation while e.g. Lulu (base=0.5, Extra1 valueless)
                    // legitimately alternates via the catch-all. Requires the cumulative
                    // selection in ObjAIBase.GetNewAutoAttack because a normalized weighted
                    // pick would roll the 2.0 slots.
                    Probability = file.GetFloat("Data", $"ExtraAttack{i}_Probability", 2.0f)
                };
                BasicAttacks[i].GetAttackValues();
                nameIndex++;
            }

            //Main Crit AutoAttack
            BasicAttacks[9] = new BasicAttackInfo(CritAttackDelayOffsetPercent, CritAttackDelayCastOffsetPercent, CritAttackDelayCastOffsetPercentAttackSpeedRatio)
            {
                Name = file.GetString("Data", $"CritAttack", ""),
                AttackCastTime = AttackCastTime,
                AttackTotalTime = AttackTotalTime,
                Probability = CritAttackProbability
            };
            BasicAttacks[9].GetAttackValues();

            //Secondary Crit AutoAttacks
            for (var i = 1; i < 9; i++)
            {
                var index = i + 9;
                var attackName = file.GetString("Data", $"ExtraCritAttack{i}", "");
                float delayOffset = file.GetFloat("Data", $"{attackName}_AttackDelayOffsetPercent", AttackDelayOffsetPercent);
                float delayCastOffsetPercent = file.GetFloat("Data", $"{attackName}_AttackDelayCastOffsetPercent", CritAttackDelayCastOffsetPercent);

                BasicAttacks[index] = new BasicAttackInfo(delayOffset, delayCastOffsetPercent, CritAttackDelayCastOffsetPercentAttackSpeedRatio)
                {
                    Name = attackName,
                    AttackCastTime = AttackCastTime,
                    AttackTotalTime = AttackTotalTime,
                    // Per-slot like S4 (catch-all 2.0 default), not the champ-level value.
                    Probability = file.GetFloat("Data", $"ExtraCritAttack{i}_Probability", 2.0f)
                };
                BasicAttacks[index].GetAttackValues();
            }

            return this;
        }
    }
}
