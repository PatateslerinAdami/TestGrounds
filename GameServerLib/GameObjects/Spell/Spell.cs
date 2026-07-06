using GameServerCore.Content;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Packets;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.Content;
using static GameServerCore.Content.HashFunctions;
using LeagueSandbox.GameServer.Logging;
using log4net;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS
{
    public class SpellCastInfo
    {
        public bool CouldCast { get; set; }
        public Vector2 Position { get; init; }
        public Vector2 EndPosition { get; init; }
        public AttackableUnit? TargetUnit { get; init; }

    }
    public class Spell: IEventSource
    {
        // Crucial Vars.
        private readonly Game _game;
        private readonly NetworkIdManager _networkIdManager;
        private float _overrrideCastRange;
        private AttackType _attackType;
        private bool _scriptActivated;
        private bool _scriptPostActivated;
        private static ILog _logger = LoggerProvider.GetLogger();

        /// <summary>
        /// General information about this spell when it is cast. Refer to CastInfo class.
        /// </summary>
        public CastInfo CastInfo { get; private set; } = new CastInfo();
        public int CurrentAmmo { get; private set; }
        public float CurrentAmmoCooldown { get; private set; }
        /// <summary>
        /// Current cooldown of this spell.
        /// </summary>
        public float CurrentCooldown { get; protected set; }
        /// <summary>
        /// Time until casting will end for this spell.
        /// </summary>
        public float CurrentCastTime { get; protected set; }
        /// <summary>
        /// Time until channeling will finish for this spell.
        /// </summary>
        public float CurrentChannelDuration { get; protected set; }

        /// <summary>
        /// Time until the same spell can be cast again. Usually only applicable to auto attack spells.
        /// </summary>
        public float CurrentDelayTime { get; protected set; }
        /// <summary>
        /// The toggle state of this spell.
        /// </summary>
        public bool Toggle { get; protected set; }
        /// <summary>
        /// Spell data for this spell used for interactions between units, cooldown, channeling time, etc. Refer to SpellData class.
        /// </summary>
        public SpellData SpellData { get; }
        /// <summary>
        /// Internal name of this spell.
        /// </summary>
        public string SpellName { get; }
        /// <summary>
        /// State of this spell. Refer to SpellState enum.
        /// </summary>
        public SpellState State { get; private set; }
        /// <summary>
        /// Script instance assigned to this spell.
        /// </summary>
        public ISpellScript Script { get; private set; }

        public uint ScriptNameHash { get; private set; }
        public IEventSource ParentScript => null;
        /// <summary>
        /// Whether or not the script for this spell is the default empty script.
        /// </summary>
        public bool HasEmptyScript { get; private set; } = true;
        /// <summary>
        /// Used to update player ability tool tip values.
        /// </summary>
        public ToolTipData ToolTipData { get; protected set; }

        /// <summary>
        /// Allocates the missile NetID for the cast about to be announced. Heroes get a fresh NetID per
        /// auto-attack (replay shows champion AAs each carry a unique missile id); non-hero units (minions,
        /// pets) reuse ONE stable NetID for all their AAs across a life — see
        /// <see cref="ObjAIBase.AutoAttackMissileNetId"/>. Non-auto-attack casts always get a fresh NetID.
        /// </summary>
        private uint GetCastMissileNetId()
        {
            if (CastInfo.IsAutoAttack && CastInfo.Owner != null && CastInfo.Owner is not Champion)
            {
                if (CastInfo.Owner.AutoAttackMissileNetId == 0)
                {
                    CastInfo.Owner.AutoAttackMissileNetId = _networkIdManager.GetNewNetId();
                }
                return CastInfo.Owner.AutoAttackMissileNetId;
            }
            return _networkIdManager.GetNewNetId();
        }

        public Spell(Game game, ObjAIBase owner, string spellName, byte slot, bool enableScripts = true)
        {
            _game = game;
            _networkIdManager = game.NetworkIdManager;
            CastInfo.MissileNetID = GetCastMissileNetId();
            _overrrideCastRange = 0;
            _attackType = AttackType.ATTACK_TYPE_RADIAL;

            State = SpellState.STATE_READY;
            CurrentAmmo = 1;
            CastInfo.Owner = owner;
            SpellName = spellName;
            CastInfo.SpellHash = (uint)GetId();
            CastInfo.AttackSpeedModifier = owner.Stats.AttackSpeedMultiplier.Total;
            CastInfo.PackageHash = owner.GetObjHash();
            CastInfo.Targets = new List<CastTarget>();
            CastInfo.SpellSlot = slot;

            CastInfo.IsSecondAutoAttack = false;

            if (CastInfo.SpellSlot >= 64)
            {
                CastInfo.IsAutoAttack = true;
            }

            try
            {
                SpellData = game.Config.ContentManager.GetSpellData(spellName);
            }
            catch (ContentNotFoundException)
            {
                SpellData = new SpellData();
            }
            if (enableScripts)
            {
                //Checks if the spell is in the passive slot, so it doesn't try to load it twice under the "Spells" and "Passives" namespaces
                if (CastInfo.SpellSlot != (int)SpellSlotType.PassiveSpellSlot)
                {
                    LoadScript();
                    // BaseSpell (Content/.../Global/BaseSpell.cs) is a content-
                    // side placeholder with no overrides; opt-in config skips its
                    // per-tick OnUpdate on the ~30 rune/extra slots every unit has.
                    HasEmptyScript = Script.GetType() == typeof(SpellScriptEmpty)
                                     || (_game.Config.TreatBaseSpellAsEmpty
                                         && Script.GetType().Name == "BaseSpell");
                }
                else
                {
                    // Passive slot uses CharScript logic; keep a non-null spell script to avoid null access in shared spell code.
                    Script = new SpellScriptEmpty();
                    HasEmptyScript = true;
                    owner.LoadCharScript(this);
                }
            }
            else
            {
                Script = new SpellScriptEmpty();
                HasEmptyScript = true;
            }

            ScriptNameHash = HashString(SpellName);

            ToolTipData = new ToolTipData(owner, this);
        }

        public int GetId()
        {
            return (int)HashFunctions.HashString(SpellName);
        }

        public void LoadScript()
        {
            ApiEventManager.RemoveAllListenersForOwner(Script);
            _scriptActivated = false;
            _scriptPostActivated = false;

            string nameSpace = "Spells";
            if (CastInfo.SpellSlot >= (byte)SpellSlotType.InventorySlots && CastInfo.SpellSlot < (byte)SpellSlotType.BluePillSlot)
            {
                nameSpace = "ItemSpells";
            }
            Script = CSharpScriptEngine.CreateObjectStatic<ISpellScript>(nameSpace, SpellName) ?? new SpellScriptEmpty();

            if (Script.ScriptMetadata.TriggersSpellCasts)
            {
                ApiEventManager.OnSpellCast.AddListener(Script, this, Script.OnSpellCast);
                ApiEventManager.OnSpellPostCast.AddListener(Script, this, Script.OnSpellPostCast);
            }

            if (GetEffectiveChannelDuration() > 0 || SpellData.UseChargeChanneling)
            {
                if (IsChargeSpell)
                {
                    // Charge-style spells: route to dedicated charge events (Varus Q etc.)
                    ApiEventManager.OnSpellChargeStart.AddListener(Script, this, Script.OnSpellChargeStart);
                    ApiEventManager.OnSpellChargeTick.AddListener(Script, this, Script.OnSpellChargeTick);
                    ApiEventManager.OnSpellChargeFire.AddListener(Script, this, Script.OnSpellChargeFire);
                    ApiEventManager.OnSpellChargeCancel.AddListener(Script, this, Script.OnSpellChargeCancel);
                }
                else
                {
                    // Normal channel-style spells (Kat R, Recall etc.)
                    ApiEventManager.OnSpellChannel.AddListener(Script, this, Script.OnSpellChannel);
                    ApiEventManager.OnSpellChannelCancel.AddListener(Script, this, Script.OnSpellChannelCancel);
                    ApiEventManager.OnSpellChannelUpdate.AddListener(Script, this, Script.OnSpellChannelUpdate);
                    ApiEventManager.OnSpellPostChannel.AddListener(Script, this, Script.OnSpellPostChannel);
                }
            }

            //Activate spell - Notes: Deactivate is never called as spell removal hasn't been added
            try
            {
                using var _scope = Profiler.Scope($"spell:{CastInfo?.Owner?.Model ?? "?"}/{SpellName}.OnActivate", "scripts");
                Script.OnActivate(CastInfo.Owner, this);
            }
            catch(Exception e)
            {
                _logger.Error(null, e);
            }

            _scriptActivated = true;
            TryPostActivateScript();
        }

        public void TryPostActivateScript()
        {
            if (_scriptPostActivated || !_scriptActivated || Script == null || CastInfo.Owner == null)
            {
                return;
            }

            bool hasViewer = false;
            foreach (var _ in CastInfo.Owner.VisibleForPlayers)
            {
                hasViewer = true;
                break;
            }

            if (!hasViewer)
            {
                return;
            }

            _scriptPostActivated = true;
            try
            {
                using var _scope = Profiler.Scope($"spell:{CastInfo?.Owner?.Model ?? "?"}/{SpellName}.OnPostActivate", "scripts");
                Script.OnPostActivate(CastInfo.Owner, this);
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }
        }

        public void ApplyEffects(AttackableUnit u, SpellMissile m = null)
        {
            if (!u.GetIsTargetableToTeam(CastInfo.Owner.Team))
            {
                return;
            }

            // Spell-shield gate — replay-verified engine behavior (project_spell_shield_system
            // memory, 3 replays 2026-07-05): a non-ally, non-AA spell execution on a target with
            // an active SPELL_SHIELD buff is blocked ENTIRELY — no OnSpellHit, no damage, no
            // debuffs, nothing on the wire except the shield's own BuffRemove2. Basic attacks
            // pass through (replay: Sona AA damages through a standing Sivir E). Executions with
            // DoesntBreakShields bypass the gate: effect applies AND the shield stays untouched
            // (SpellMetaData.txt: "the spell's execution won't break shields" — Absolute Zero,
            // TormentedSoil ticks, sub-executions like KarthusLayWasteDead*).
            if (!CastInfo.IsAutoAttack
                && Script.ScriptMetadata?.DoesntBreakShields != true
                && u.ConsumeSpellShield(this))
            {
                return;
            }
            if (SpellData.HaveHitEffect && !string.IsNullOrEmpty(SpellData.HitEffectName) && !CastInfo.IsAutoAttack && HasEmptyScript)
            {
                if (SpellData.HaveHitBone)
                {
                    ApiFunctionManager.AddParticleTarget(CastInfo.Owner, null, SpellData.HitEffectName, u, targetBone: SpellData.HitBoneName, lifetime: 1.0f);
                }
                else
                {
                    ApiFunctionManager.AddParticleTarget(CastInfo.Owner, null, SpellData.HitEffectName, u, lifetime: 1.0f);
                }
            }

            if (CastInfo.IsAutoAttack)
            {
                ApiEventManager.OnBeingHit.Publish(u, CastInfo.Owner);
            }
            else
            {
                ApiEventManager.OnSpellHit.Publish(this, (u, m));
                if (m != null)
                {
                    ApiEventManager.OnSpellMissileHit.Publish(m, u);
                    // Drives the client's on-hit FX + WWise audio path (Spells/<name>/hit).
                    _game.PacketNotifier.NotifyS2C_LineMissileHitList(m, u);
                }
                ApiEventManager.OnBeingSpellHit.Publish(u, (this, m));
            }
        }

        private bool ShouldTreatAsAutoAttack()
        {
            if (CastInfo.IsAutoAttack)
            {
                return true;
            }

            return CastInfo.Owner is ObjAIBase ai && ai.AutoAttackSpell == this;
        }

        private bool IsBasicAttackSlotSpell()
        {
            return CastInfo.SpellSlot >= (byte)SpellSlotType.BasicAttackNormalSlots;
        }

        /// <summary>
        /// Whether or not this spell can be cancelled during cast.
        /// </summary>
        /// <returns></returns>
        public bool CastCancelCheck()
        {
            if (CastInfo.Owner.IsDead
            && !SpellData.CanOnlyCastWhileDead)
            {
                ResetSpellCast();
                return true;
            }

            if (CastInfo.Targets.Count > 0 && CastInfo.Targets[0].Unit != null)
            {
                // Uncancellable.
                if (SpellData.CantCancelWhileWindingUp)
                {
                    return false;
                }

                var spellTarget = CastInfo.Targets[0].Unit;

                if (!spellTarget.IsVisibleByTeam(CastInfo.Owner.Team)
                || !spellTarget.Status.HasFlag(StatusFlags.Targetable)
                || !spellTarget.GetIsTargetableToTeam(CastInfo.Owner.Team)
                || spellTarget.IsDead)
                {
                    if (CastInfo.IsAutoAttack)
                    {
                        CastInfo.Owner.CancelAutoAttack(true);
                        return true;
                    }

                    ResetSpellCast();
                    return true;
                }

                // Regular auto attacks can lose their target due to untargetability and distance.
                // ar_StopAttackRangeModifier (LEVELS/*/Constants.var:62, default 100) — the
                // slack behind the Lua AI API `TargetInCancelAttackRange`; mirrors the twin
                // check in ObjAIBase.UpdateTarget (both sourced from GlobalData, not hardcoded).
                // Edge-to-edge RESOLVED: both collision radii MUST be included — engage range is
                // edge-to-edge (S4 GetClosestAttackPoint: range + both radii), so a center-to-center
                // cancel range would undercut the engage range for large-radius targets (Baron/Cho
                // radius alone can exceed the slack) and cancel windups on still-attackable targets.
                float cancelBuffer = GlobalData.AttackRangeVariables.StopAttackRangeModifier;
                float maxCancelRange = CastInfo.Owner.Stats.Range.Total + spellTarget.CollisionRadius + CastInfo.Owner.CollisionRadius + cancelBuffer;
                if (CastInfo.IsAutoAttack
                && (spellTarget != CastInfo.Owner.TargetUnit
                || Vector2.Distance(spellTarget.Position, CastInfo.Owner.Position) > maxCancelRange
                || CastInfo.Owner.GetCastSpell() != null
                || CastInfo.Owner.ChannelSpell != null))
                {
                    CastInfo.Owner.CancelAutoAttack(!CastInfo.Owner.HasAutoAttacked, true);
                    return true;
                }
            }
            else
            {
                if (CastInfo.IsAutoAttack)
                {
                    CastInfo.Owner.CancelAutoAttack(true);
                    return true;
                }
            }


            var status = CastInfo.Owner.Status;

            // M2 Phase 3: CC clears the relevant capability (BuffType.ToCapabilityDisable) — an in-progress
            // AA is interrupted when CanAttack clears, a spell cast when CanCast clears. (Taunt disables cast
            // but not attack, so a taunted unit keeps auto-attacking — faithful.)
            if ((CastInfo.IsAutoAttack && !status.HasFlag(StatusFlags.CanAttack))
             || (!CastInfo.IsAutoAttack && !status.HasFlag(StatusFlags.CanCast)))
            {
                ResetSpellCast();
                return true;
            }

            return false;
        }

        public bool Cast(Vector2 start, Vector2 end, AttackableUnit unit = null)
        {
            if ((unit == null && SpellData.TargetingType == TargetingType.Target)
                || (CastInfo.Owner.MovementParameters != null && !SpellData.CanCastWhileDisabled))
            {
                return false;
            }

            if (unit == null
                && (SpellData.TargetingType == TargetingType.Self
                || SpellData.TargetingType == TargetingType.SelfAOE
                || SpellData.TargetingType == TargetingType.TargetOrLocation))
            {
                unit = CastInfo.Owner;
            }

            _attackType = AttackType.ATTACK_TYPE_RADIAL;
            var stats = CastInfo.Owner.Stats;

            // Auto-attack overrides can use non-AA slots (ex: TalonNoxianDiplomacyAttack).
            // If this spell is currently selected as the owner's attack spell, keep AA behavior.
            CastInfo.IsAutoAttack = ShouldTreatAsAutoAttack();

            if ((SpellData.ManaCost[CastInfo.SpellLevel] * (1 - stats.SpellCostReduction) > stats.CurrentMana && !CastInfo.IsAutoAttack) || State != SpellState.STATE_READY || CurrentAmmo <= 0)
            {
                return false;
            }

            CastInfo.SpellNetID = _networkIdManager.GetNewNetId();

            // Fresh per-cast variable bag (Riot: new cast = new LuaVars table). In-flight
            // missiles of the PREVIOUS cast keep the old bag — their cloned CastInfo still
            // references it — so overlapping casts never share per-cast state.
            CastInfo.Variables = new BuffVariables();

            CastInfo.AttackSpeedModifier = stats.AttackSpeedMultiplier.Total;

            CastInfo.MissileNetID = GetCastMissileNetId();

            CastInfo.TargetPosition = new Vector3(start.X, _game.Map.NavigationGrid.GetHeightAtLocation(start), start.Y);
            CastInfo.TargetPositionEnd = new Vector3(end.X, _game.Map.NavigationGrid.GetHeightAtLocation(end), end.Y);
            // CursorPos = the raw aim point at cast time (Riot SpellCastInfo::CursorPos). Captured
            // here from the unmodified click before any later fakePos / target-snap overwrites
            // TargetPosition, so it preserves where the player actually pointed.
            CastInfo.CursorPos = CastInfo.TargetPosition;

            CastInfo.Targets.Clear();

            // TODO: Unhardcode (wind down? If so, make it cancelable via casting a different spell and via changing move order to AttackTo or MoveTo.)
            CastInfo.ExtraCastTime = 0.0f;
            CastInfo.Cooldown = GetCooldown();
            // TODO: Unhardcode (extra windup?)
            CastInfo.StartCastTime = 0.0f;

            var trueChannel = GetEffectiveChannelDuration();

            // Spells cast through the NORMAL pipeline (not attack slots) whose timing must
            // still be the champion's attack timing: windup = AA cast delay, total = full
            // AA cycle, both AS-scaled. Data flags: UseAutoattackCastTime (Yasuo Q+W
            // variants, ItemTiamatCleave, Jinx Q attacks) and ConsideredAsAutoAttack
            // (TF cards, Draven spinning axes, Parley). Replay-verified (630b7ceb): Yasuo
            // QW at SLOT 0 sends ct=0.318/tt=1.448 = exactly windup/cycle, ct/tt constant
            // per champion (= AttackDelayCastPercent sum), tt shrinking with live AS.
            if (!CastInfo.IsAutoAttack && (SpellData.ConsideredAsAutoAttack || SpellData.UseAutoattackCastTime))
            {
                CastInfo.DesignerCastTime = SpellData.GetCharacterAttackCastDelay(CastInfo.AttackSpeedModifier, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayCastOffsetPercent, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayCastOffsetPercentAttackSpeedRatio, CastInfo.Owner.MaxAttackSpeedOverride, CastInfo.Owner.MinAttackSpeedOverride);
                CastInfo.DesignerTotalTime = SpellData.GetCharacterAttackDelay(CastInfo.AttackSpeedModifier, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent, CastInfo.Owner.MaxAttackSpeedOverride, CastInfo.Owner.MinAttackSpeedOverride);
                CastInfo.UseAttackCastDelay = true;
            }
            else
            {
                CastInfo.DesignerCastTime = SpellData.GetCastTime();
                // For non-channel spells, DesignerTotalTime mirrors GetCastTimeTotal() = (1 +
                // DelayTotalTimePercent) * 2.0 this is verified across Q/W/E/R replay CastSpellAns
                // packets where every Katarina basic spell has total=1.0 and channel R has
                // total=cast+channel=2.401. The client uses DesignerTotalTime as the input-
                // lockout window: total=cast+0=0.25 lets the next spell click reach the server
                // ~270ms after E starts, racing E's spell3 anim end at ~273ms; total=1.0 holds
                // the lockout for the full second so subsequent casts are clean.
                CastInfo.DesignerTotalTime = trueChannel > 0
                    ? CastInfo.DesignerCastTime + trueChannel
                    : SpellData.GetCastTimeTotal();
            }

            if (SpellData.OverrideCastTime > 0)
            {
                CastInfo.DesignerCastTime = SpellData.OverrideCastTime;
                CastInfo.DesignerTotalTime = trueChannel > 0
                    ? SpellData.OverrideCastTime + trueChannel
                    : SpellData.GetCastTimeTotal();
            }

            if (Script.ScriptMetadata.CastTime > 0)
            {
                CastInfo.DesignerCastTime = Script.ScriptMetadata.CastTime;
                CastInfo.DesignerTotalTime = trueChannel > 0
                    ? Script.ScriptMetadata.CastTime + trueChannel
                    : SpellData.GetCastTimeTotal();
            }

            // Otherwise, use the normal auto attack setup
            if (CastInfo.IsAutoAttack)
            {
                _attackType = AttackType.ATTACK_TYPE_TARGETED;
                CastInfo.UseAttackCastTime = true;
                CastInfo.IsSecondAutoAttack = CastInfo.Owner.HasMadeInitialAttack;
            }

            // Replay-verified (630b7ceb, 2599 ANS): AmmoUsed = 1 on EVERY cast — including
            // auto-attack overrides (JinxQAttack, CaitlynHeadshotMissile). AmmoRechargeTime
            // is 0 for everything except real ammo spells (AkaliShadowDance: 28.5s recharge
            // alongside its 1.9s between-cast cooldown).
            CastInfo.AmmoUsed = 1;
            // The client reads this from the CastSpellAns to drive the charge-recharge display
            // (replay VelkozW: AmmoRecharge=17.1 = the full recharge time, sent on every cast).
            // Must be the recharge DURATION, not CurrentAmmoCooldown (which is 0 before the consume).
            CastInfo.AmmoRechargeTime = GetAmmoRechageTime();

            // TODO: Account for multiple targets
            CastInfo.Targets.Add(new CastTarget(unit, CastTarget.GetHitResult(unit, CastInfo.IsAutoAttack, CastInfo.Owner.IsNextAutoCrit, CastInfo.Owner.IsNextAutoMiss, CastInfo.Owner.IsNextAutoDodged)));

            // TODO: implement check for IsForceCastingOrChannel and IsOverrideCastPosition
            if (SpellData.CastType == (int)CastType.CAST_TargetMissile
             || SpellData.CastType == (int)CastType.CAST_ChainMissile)
            {
                // TODO: Verify
                CastInfo.IsClickCasted = true;
            }

            // TODO: Verify
            CastInfo.SpellCastLaunchPosition = CastInfo.Owner.GetPosition3D();

            var targetingType = SpellData.TargetingType;
            if (!CastInfo.IsAutoAttack
             && (targetingType == TargetingType.Target
             || targetingType == TargetingType.Area
             || targetingType == TargetingType.Location
             || targetingType == TargetingType.DragDirection))
            {
                var distance = Vector2.DistanceSquared(CastInfo.Owner.Position, start);
                var castRange = GetCurrentCastRange();

                if (targetingType == TargetingType.Target)
                {
                    _attackType = AttackType.ATTACK_TYPE_TARGETED;
                    distance = Vector2.DistanceSquared(CastInfo.Owner.Position, unit.Position);

                    if (distance > castRange * castRange)
                    {
                        CastInfo.Owner.SetSpellToCast(this, Vector2.Zero, unit);
                        return false;
                    }
                }

                if (distance > castRange * castRange)
                {
                    CastInfo.Owner.SetSpellToCast(this, end);
                    return false;
                }
            }

            // All spell checks and steps passed, set the casting spell on the owner.
            // TODO: Verify if we should also do this for manual SpellCasts
            if (!CastInfo.IsAutoAttack)
            {
                if (!SpellData.DoesntBreakChannels)
                {
                    CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.Casting);
                }

                if (!SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                {
                    CastInfo.Owner.SetCastSpell(this);
                }
                CastInfo.Owner.AutoAttackSpell.CastCancelCheck();
            }
            // Prevents overriding current auto attack target
            else if (unit != null)
            {
                CastInfo.Owner.SetTargetUnit(unit, true);
            }

            try
            {
                using var _scope = Profiler.Scope($"spell:{CastInfo?.Owner?.Model ?? "?"}/{SpellName}.OnSpellPreCast", "scripts");
                Script.OnSpellPreCast(CastInfo.Owner, this, unit, start, end);
            }
            catch(Exception e)
            {
                _logger.Error(null, e);
            }
            if (CastInfo.Owner.Status.HasFlag(StatusFlags.Stealthed) && Script.ScriptMetadata.CastingBreaksStealth)
            {
                var packetInfo = new DelayedSpellPacketInfo(this, _game.GameTime);
                CastInfo.Owner.delayedSpellPackets.Add(packetInfo);
                CastInfo.Owner.RemoveBuffsByType(BuffType.INVISIBILITY);
            }
            if (_game.Config.GameFeatures.HasFlag(FeatureFlags.EnableManaCosts))
            {
                stats.CurrentMana -= SpellData.ManaCost[CastInfo.SpellLevel] * (1 - stats.SpellCostReduction);
            }

            if (!CastInfo.IsAutoAttack && !SpellData.IsToggleSpell
                        || (!SpellData.NoWinddownIfCancelled
                        && !SpellData.Flags.HasFlag(SpellDataFlags.InstantCast)
                        && SpellData.CantCancelWhileWindingUp))
            {
                if (!SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                {
                    if (!SpellData.CanMoveWhileChanneling)
                    {
                        // Replay-verified (Jinx W, 143 casts): when a movement-locking cast
                        // starts while the unit is moving, Riot broadcasts a 1-waypoint
                        // WaypointGroup stop for the caster in the cast tick — without it the
                        // client keeps walking the stale path during the windup ("sliding"
                        // forward while cast-locked). networked:true routes through the safe
                        // batched flush (no direct Notify — see gray-line fix in StopMovement).
                        // Dash guard: never cancel an active forced movement from the generic
                        // cast pipeline (OnSpellPreCast at line ~535 may have started one).
                        if (CastInfo.Owner.MovementParameters == null)
                        {
                            CastInfo.Owner.StopMovement(MoveStopReason.Finished);
                        }
                        // TODO: Verify if we should move this outside of this TriggersSpellCasts if statement.
                        CastInfo.Owner.UpdateMoveOrder(OrderType.CastSpell, true);
                    }

                    if (Script.ScriptMetadata.AutoFaceDirection && CastInfo.Owner is not BaseTurret)
                    {
                        var goingTo = end - CastInfo.Owner.Position;

                        if (unit != null)
                        {
                            goingTo = unit.Position - CastInfo.Owner.Position;
                        }

                        var dirTemp = Vector2.Normalize(goingTo);
                        CastInfo.Owner.FaceDirection(new Vector3(dirTemp.X, 0, dirTemp.Y), false);
                    }
                }
            }
            else if (CastInfo.IsAutoAttack
                     && !SpellData.Flags.HasFlag(SpellDataFlags.InstantCast)
                     && !SpellData.CanMoveWhileChanneling
                     && CastInfo.Owner.MovementParameters == null
                     && !CastInfo.Owner.IsPathEnded())
            {
                // Riot StopActor on auto-attack windup (decomp Spellbook.cpp:705 / AIBase.cpp:1501): when
                // the windup begins the unit plants in place — its path is cleared and a Stop is sent to
                // clients. Without this the client keeps walking the stale chase path while the swing
                // animation plays ("sliding forward during the attack" when the target runs away). This
                // is the same sliding fix already applied to movement-locking SPELL casts in the branch
                // above; an auto-attack is excluded from that branch (it is cancelable while winding up,
                // CantCancelWhileWindingUp=false), so it needs its own stop here.
                //
                // Unlike a real spell cast we deliberately do NOT set OrderType.CastSpell: the attack
                // must leave the move order intact so the engine re-chases a fleeing target between
                // swings (stutter-step), and so a fresh move order during the windup still cancels the
                // attack (orb-walk). Dash guard (MovementParameters == null) mirrors the spell branch.
                CastInfo.Owner.StopMovement(MoveStopReason.Finished);
            }

            // If we are supposed to automatically cast a skillshot for this spell, then calculate the proper end position before casting.
            // SERVER-INTERNAL heuristic (not wire-verifiable — Riot's server logic is not
            // visible): both Circle and Arc are location-targeted skillshot missiles
            // (Circle: e.g. Diana W orbs; Arc: straight skillshots after the JSON-CastType
            // cleanup). Originally Circle-only; Arc added so retyped skillshots keep their
            // auto-computed end position.
            if (Script.ScriptMetadata.MissileParameters != null
                && (Script.ScriptMetadata.MissileParameters.Type == MissileType.Circle
                 || Script.ScriptMetadata.MissileParameters.Type == MissileType.Arc))
            {
                var targetPos = ApiFunctionManager.GetPointFromUnit(CastInfo.Owner, GetCurrentCastRange());
                CastInfo.TargetPosition = new Vector3(targetPos.X, _game.Map.NavigationGrid.GetHeightAtLocation(targetPos), targetPos.Y);
                // TODO: Verify if we should also override TargetPositionEnd (probably not due to things like Viktor E).
            }

            if (CastInfo.IsAutoAttack && CastInfo.Owner.IsMelee)
            {
                _attackType = AttackType.ATTACK_TYPE_MELEE;
            }

            if (CastInfo.Targets.Count > 0 && CastInfo.Targets[0].Unit != null && CastInfo.Targets[0].Unit != CastInfo.Owner)
            {
                // Minions face their attack target via S2C_UnitSetLookAt (0x10F) only (sent on the
                // cast's LookAt point). Riot sends ZERO standalone S2C_FaceDirection (0x50) for minions
                // (replay a6db3774 diff: Riot 0x50/minion = 0; ours was ~11/minion = one redundant
                // facing packet per auto-attack on top of the LookAt). Suppress it for minions to match
                // Riot's facing wire; champions/others keep FaceDirection.
                if (Script.ScriptMetadata.AutoFaceDirection && CastInfo.Owner is not BaseTurret
                    && CastInfo.Owner is not Minion)
                {
                    ApiFunctionManager.FaceDirection(CastInfo.Targets[0].Unit.Position, CastInfo.Owner);
                }
            }

            if (CastInfo.IsAutoAttack || CastInfo.UseAttackCastTime)
            {
                // We assume it is already an attack.
                int index = CastInfo.SpellSlot - 64;
                if (CastInfo.SpellSlot >= 45 && CastInfo.SpellSlot <= 60)
                {
                    // Extra Spells which UseAttackCastTime just use the base auto attack's cast time.
                    index = 0;
                }

                // Replay-verified (TalonNoxianDiplomacyAttack CastSpellAns, 56 casts): both the
                // windup and the total scale with the LIVE attack-speed multiplier — the ratio
                // ct/tt stays exactly (AttackDelayCastPercent + AttackDelayCastOffsetPercent)
                // (0.2005 for Talon) while tt = baseCycle / attackSpeedMod (measured
                // 1.533 / 1.20..1.76). Unscaled values made the client animate empowered-AA
                // cast frames too slowly at high attack speed.
                float attackSpeedMod = Math.Max(0.0001f, CastInfo.AttackSpeedModifier);
                float autoAttackTotalTime = GlobalData.GlobalCharacterDataConstants.AttackDelay * (1.0f + CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent) / attackSpeedMod;
                // Windup = client Spell::ComputeCharacterAttackCastDelay: a Lerp between the unscaled
                // and AS-scaled cast time governed by the per-slot AttackSpeedRatio. ratio=1 (Talon,
                // Jinx — replay-verified) collapses to autoAttackTotalTime * castPercent (the old
                // formula, unchanged); ratio<1 (Thresh, Kalista) keeps the windup partially unscaled
                // so it doesn't shrink too far at high attack speed.
                CastInfo.DesignerCastTime = SpellData.GetCharacterAttackCastDelay(
                    attackSpeedMod,
                    CastInfo.Owner.CharData.BasicAttacks[index].AttackDelayOffsetPercent,
                    CastInfo.Owner.CharData.BasicAttacks[index].AttackDelayCastOffsetPercent,
                    CastInfo.Owner.CharData.BasicAttacks[index].AttackDelayCastOffsetPercentAttackSpeedRatio,
                    CastInfo.Owner.MaxAttackSpeedOverride,
                    CastInfo.Owner.MinAttackSpeedOverride);

                if (CastInfo.IsAutoAttack && IsBasicAttackSlotSpell())
                {
                    // Minions/pets auto-attack AUTONOMOUSLY on the client. Once a Basic_Attack puts them
                    // into the hardcode-attack state, obj_AI_Minion::UpdatePimpl (mac decomp 4.17,
                    // AIMinionClient.cpp:138) keeps re-firing the swing at the client's own computed cadence
                    // with NO further packets. So Riot sends a minion/pet a Basic_Attack only on target
                    // ACQUISITION/CHANGE, never per swing (replay 26ec2d65: Tibbers = 7 Basic_Attacks but 19
                    // hit/damage events, target NetID changing each packet). Sending one per swing made the
                    // client render OUR packet-driven attack PLUS its own autonomous one → the hit FX fired
                    // twice (the "double AA hit FX on pets" bug, reproduces even stationary).
                    // !IsSecondAutoAttack (== !HasMadeInitialAttack, which resets on (re)target) is exactly
                    // the first attack against a freshly acquired target = our "target changed" edge.
                    // Champions (obj_AI_Hero) have NO autonomous attack loop, so they still need a packet
                    // every swing: the position-less Basic_Attack (0x0C) chain for follow-ups.
                    if (!CastInfo.IsSecondAutoAttack)
                    {
                        _game.PacketNotifier.NotifyBasic_Attack_Pos(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit);
                    }
                    else if (CastInfo.Owner is not Minion)
                    {
                        _game.PacketNotifier.NotifyBasic_Attack(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit, CastInfo.Owner.HasMadeInitialAttack);
                    }
                    // else: minion/pet continuation swing on the same target — send nothing; the client's
                    // autonomous minion attack loop renders it (sending here would double the hit FX).
                }

                // Riot wire: DesignerTotalTime of EVERY attack-timed cast = the full
                // (AS-scaled) attack cycle, never ct + channel. Verified twice:
                // TalonNoxianDiplomacyAttack (56 casts, tt = baseCycle/ASmod) and replay
                // 630b7ceb (JinxQAttack family: tt*ASmod constant 1.600 across all AS
                // values). The old "UseAttackCastTime ? ct + trueChannel" branch shadowed
                // this for all attacks since UseAttackCastTime is set alongside
                // IsAutoAttack — sending windup-only totals.
                CastInfo.DesignerTotalTime = autoAttackTotalTime + trueChannel;
            }

            if (CastInfo.Targets.Count > 0 && CastInfo.Targets[0].Unit != null && CastInfo.Targets[0].Unit != CastInfo.Owner)
            {
                _game.PacketNotifier.NotifyS2C_UnitSetLookAt(CastInfo.Owner, CastInfo.Targets[0].Unit, _attackType);
            }

            if (!CastInfo.IsAutoAttack)
            {
                if(SpellData.MaxAmmo > 1)
                {
                    CurrentAmmo--;

                    // Riot obj_AI_Base::ApplySpellCastCosts: on consuming a charge, arm the recharge
                    // timer ONLY if no recharge is already in flight (an in-progress refill keeps
                    // counting). Without arming it the recharge tick (UpdateAmmo) would see
                    // CurrentAmmoCooldown <= 0 and refill the charge on the very next tick.
                    if (CurrentAmmoCooldown <= 0.0f)
                    {
                        CurrentAmmoCooldown = GetAmmoRechageTime();
                    }
                    // NO NotifyS2C_AmmoUpdate here — replay (VelkozW, c0896952) shows Riot does NOT
                    // send S2C_AmmoUpdate on the cast-consume tick; the client learns the consume from
                    // the CastSpellAns (AmmoUsed=1 + AmmoRechargeTime). Sending it here (with the
                    // recharge time) made the client display the spell on the full ~18s ammo-recharge
                    // cooldown, blocking the remaining charge. S2C_AmmoUpdate only fires on recharge
                    // events (AddAmmo). OnUpdateAmmo (the internal script callback) still fires here,
                    // mirroring Riot's HandleOnAmmoUpdate on consume.
                    ApiEventManager.OnUpdateAmmo.Publish(CastInfo.Owner, this);
                }
                Vector3 originalTarget = CastInfo.TargetPosition;
                Vector3 originalTargetEnd = CastInfo.TargetPositionEnd;

                if (!Script.ScriptMetadata.AutoFaceDirection && !Script.ScriptMetadata.OverrideTargetPositionInScript)
                {
                    var owner = CastInfo.Owner;
                    // Guard against Vector2.Normalize(Vector2.Zero) which returns (NaN, NaN)
                    // instead of Vector2.Zero. A zero Direction (fresh-spawned unit that
                    // has never moved/faced) would otherwise propagate NaN into the
                    // fakePos broadcast and corrupt the client's CastInfo.
                    var dirXZ = new Vector2(owner.Direction.X, owner.Direction.Z);
                    var forwardVec = dirXZ == Vector2.Zero ? new Vector2(1, 0) : Vector2.Normalize(dirXZ);

                    var fakePos = owner.Position + (forwardVec * 10.0f);
                    var fakePos3D = new Vector3(fakePos.X, _game.Map.NavigationGrid.GetHeightAtLocation(fakePos), fakePos.Y);

                    CastInfo.TargetPosition = fakePos3D;
                    CastInfo.TargetPositionEnd = fakePos3D;
                }
                _game.PacketNotifier.NotifyNPC_CastSpellAns(this);
                if (SpellData.CanMoveWhileChanneling && !CastInfo.Owner.IsPathEnded())
                {
                    _game.PacketNotifier.NotifyWaypointGroup(CastInfo.Owner);
                }
            }

            // Auto-attack overrides in non-basic slots still need a normal cast packet for animation.
            if (CastInfo.IsAutoAttack && !IsBasicAttackSlotSpell())
            {
                _game.PacketNotifier.NotifyNPC_CastSpellAns(this);
            }

            if (CastInfo.DesignerCastTime > 0)
            {
                if (Script.ScriptMetadata.TriggersSpellCasts)
                {
                    ApiEventManager.OnSpellCast.Publish(this);
                }

                if (CastInfo.IsAutoAttack)
                {
                    ApiEventManager.OnPreAttack.Publish(CastInfo.Owner, this);
                }

                if (!CastInfo.UseAttackCastDelay)
                {
                    if (!SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                    {
                        State = SpellState.STATE_CASTING;
                    }
                    else
                    {
                        FinishCasting();
                    }
                }
                else
                {
                    State = SpellState.STATE_CASTING;
                }
                CurrentCastTime = CastInfo.DesignerCastTime;
            }
            else
            {
                FinishCasting();
                if (GetEffectiveChannelDuration() > 0)
                {
                    Channel();
                }
            }

            return true;
        }
        public bool Cast(CastInfo castInfo, bool cast, bool isContinuation = false)
        {
            CastInfo = castInfo;
            CastInfo.IsAutoAttack = ShouldTreatAsAutoAttack();
            /*
            if (CastInfo.Targets == null)
            {
                CastInfo.Targets = new List<CastTarget>();
            }
            if (CastInfo.Targets.Count == 0)
            {
                CastInfo.Targets.Add(new CastTarget(null, HitResult.HIT_Normal));
            }
            */
            var start = new Vector2(CastInfo.TargetPosition.X, CastInfo.TargetPosition.Z);
            var end = new Vector2(CastInfo.TargetPositionEnd.X, CastInfo.TargetPositionEnd.Z);

            try
            {
                using var _scope = Profiler.Scope($"spell:{CastInfo?.Owner?.Model ?? "?"}/{SpellName}.OnSpellPreCast", "scripts");
                Script.OnSpellPreCast(CastInfo.Owner, this, castInfo.Targets[0].Unit, start, end);
            }
            catch(Exception e)
            {
                _logger.Error(null, e);
            }

            var stats = CastInfo.Owner.Stats;

            if (cast)
            {
                if ((SpellData.ManaCost[CastInfo.SpellLevel] * (1 - stats.SpellCostReduction) > stats.CurrentMana && !CastInfo.IsAutoAttack) || State != SpellState.STATE_READY)
                {
                    return false;
                }

                if (_game.Config.GameFeatures.HasFlag(FeatureFlags.EnableManaCosts))
                {
                    stats.CurrentMana -= SpellData.ManaCost[CastInfo.SpellLevel] * (1 - stats.SpellCostReduction);
                }
            }

            CastInfo.MissileNetID = GetCastMissileNetId();

            // 0 is the correct default for both: replay 630b7ceb shows 2541/2599 ANS with
            // ExtraCastTime = StartCastTime = 0. The two non-zero patterns (not implemented):
            //  (a) small negative ExtraCastTime (−0.001..−0.03, StartCastTime 0) exclusively
            //      on auto-attack override casts — attack-cycle drift compensation that
            //      shortens the next windup by the previous attack's overshoot;
            //  (b) StartCastTime > 0 with ExtraCastTime = −StartCastTime (e.g. RocketGrab
            //      +0.259/−0.259, KhazixWLong +0.194/−0.194) — an in-progress cast announced
            //      late (likely vision-acquire mid-windup): the client fast-forwards the
            //      windup by StartCastTime.
            CastInfo.ExtraCastTime = 0.0f;
            CastInfo.Cooldown = GetCooldown();
            CastInfo.StartCastTime = 0.0f;

            var trueChannel = GetEffectiveChannelDuration();

            // Spells cast through the NORMAL pipeline (not attack slots) whose timing must
            // still be the champion's attack timing: windup = AA cast delay, total = full
            // AA cycle, both AS-scaled. Data flags: UseAutoattackCastTime (Yasuo Q+W
            // variants, ItemTiamatCleave, Jinx Q attacks) and ConsideredAsAutoAttack
            // (TF cards, Draven spinning axes, Parley). Replay-verified (630b7ceb): Yasuo
            // QW at SLOT 0 sends ct=0.318/tt=1.448 = exactly windup/cycle, ct/tt constant
            // per champion (= AttackDelayCastPercent sum), tt shrinking with live AS.
            if (!CastInfo.IsAutoAttack && (SpellData.ConsideredAsAutoAttack || SpellData.UseAutoattackCastTime))
            {
                CastInfo.DesignerCastTime = SpellData.GetCharacterAttackCastDelay(CastInfo.AttackSpeedModifier, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayCastOffsetPercent, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayCastOffsetPercentAttackSpeedRatio, CastInfo.Owner.MaxAttackSpeedOverride, CastInfo.Owner.MinAttackSpeedOverride);
                CastInfo.DesignerTotalTime = SpellData.GetCharacterAttackDelay(CastInfo.AttackSpeedModifier, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent, CastInfo.Owner.MaxAttackSpeedOverride, CastInfo.Owner.MinAttackSpeedOverride);
                CastInfo.UseAttackCastDelay = true;
            }
            else
            {
                CastInfo.DesignerCastTime = SpellData.GetCastTime();
                // For non-channel spells, DesignerTotalTime mirrors GetCastTimeTotal() = (1 +
                // DelayTotalTimePercent) * 2.0 this verified across Q/W/E/R replay CastSpellAns
                // packets where every Katarina basic spell has total=1.0 and channel R has
                // total=cast+channel=2.401. The client uses DesignerTotalTime as the input-
                // lockout window: total=cast+0=0.25 lets the next spell click reach the server
                // ~270ms after E starts, racing E's spell3 anim end at ~273ms; total=1.0 holds
                // the lockout for the full second so subsequent casts are clean.
                CastInfo.DesignerTotalTime = trueChannel > 0
                    ? CastInfo.DesignerCastTime + trueChannel
                    : SpellData.GetCastTimeTotal();
            }

            if (SpellData.OverrideCastTime > 0)
            {
                CastInfo.DesignerCastTime = SpellData.OverrideCastTime;
                CastInfo.DesignerTotalTime = trueChannel > 0
                    ? SpellData.OverrideCastTime + trueChannel
                    : SpellData.GetCastTimeTotal();
            }

            if (Script.ScriptMetadata.CastTime > 0)
            {
                CastInfo.DesignerCastTime = Script.ScriptMetadata.CastTime;
                CastInfo.DesignerTotalTime = trueChannel > 0
                    ? Script.ScriptMetadata.CastTime + trueChannel
                    : SpellData.GetCastTimeTotal();
            }

            // Otherwise, use the normal auto attack setup
            if (CastInfo.IsAutoAttack)
            {
                CastInfo.UseAttackCastTime = true;
                CastInfo.IsSecondAutoAttack = CastInfo.Owner.HasMadeInitialAttack;
            }

            // Replay-verified (630b7ceb, 2599 ANS): AmmoUsed = 1 on EVERY cast incl.
            // auto-attack overrides; AmmoRechargeTime = the AMMO recharge (0 for non-ammo
            // spells) — the previous CurrentCooldown here would have leaked every spell's
            // cooldown into the field, which Riot never does.
            CastInfo.AmmoUsed = 1;
            // The client reads this from the CastSpellAns to drive the charge-recharge display
            // (replay VelkozW: AmmoRecharge=17.1 = the full recharge time, sent on every cast).
            // Must be the recharge DURATION, not CurrentAmmoCooldown (which is 0 before the consume).
            CastInfo.AmmoRechargeTime = GetAmmoRechageTime();

            // TODO: implement check for IsForceCastingOrChannel and IsOverrideCastPosition
            if (SpellData.CastType == (int)CastType.CAST_TargetMissile
             || SpellData.CastType == (int)CastType.CAST_ChainMissile)
            {
                // TODO: Verify
                CastInfo.IsClickCasted = true;
            }

            // TODO: Verify
            _attackType = AttackType.ATTACK_TYPE_RADIAL;

            if (cast && (!CastInfo.IsAutoAttack && !SpellData.IsToggleSpell
                        || (!SpellData.NoWinddownIfCancelled
                        && !SpellData.Flags.HasFlag(SpellDataFlags.InstantCast)
                        && SpellData.CantCancelWhileWindingUp)))
            {
                if (!CastInfo.IsAutoAttack && !SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                {
                    CastInfo.Owner.SetCastSpell(this);
                }

                // Casting a real ability interrupts an in-progress auto-attack windup
                // (League: abilities cancel the AA windup before its damage point). The
                // player-path Cast already does this via AutoAttackSpell.CastCancelCheck()
                // (which cancels once GetCastSpell() != null); the API/forced path used by
                // sub-casts like JinxWMissile (cast from JinxW.OnSpellPostCast) was missing
                // it, so the AA and the new cast BOTH sat in STATE_CASTING with IsAttacking
                // stuck true: the client kept the AA animation track locked (Jinx's W stance
                // never played, stayed in rocket stance), the missile cast-frame fired early
                // (client showed the missile before the server windup ended), and the attack
                // pipeline ping-ponged (windup restarting + instantly cancelling, no attacks
                // until a form swap). Mirror the player path here.
                CastInfo.Owner.AutoAttackSpell?.CastCancelCheck();

                if (Script.ScriptMetadata.TriggersSpellCasts || GetEffectiveChannelDuration() > 0)
                {
                    if (!SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                    {
                        if (!SpellData.CanMoveWhileChanneling)
                        {
                            // Replay-verified (Jinx W, 143 casts): Riot broadcasts a 1-waypoint
                            // WaypointGroup stop for the caster in the cast tick. This overload is
                            // the SpellCast/forced path — without the stop here, a sub-cast like
                            // JinxWMissile (cast from JinxW.OnSpellPostCast) sends no stop packet
                            // and the client keeps walking its stale path during the 0.6s windup
                            // ("sliding"). The parent JinxW is InstantCast-flagged, so the player-
                            // path stop above never covers it. cast=false (fireWithoutCasting,
                            // e.g. Talon blades) correctly skips this whole block.
                            // Dash guard: only clear the WALKING path — never cancel an active
                            // forced movement (StopMovement would SetForceMovementState(false)).
                            // Scripts may start a dash in OnSpellPreCast (runs before this
                            // block, e.g. FioraDanceStrike's windup mini-dash) and manage its
                            // end themselves.
                            if (CastInfo.Owner.MovementParameters == null)
                            {
                                CastInfo.Owner.StopMovement(MoveStopReason.Finished);
                            }
                            // TODO: Verify if we should move this outside of this TriggersSpellCasts if statement.
                            CastInfo.Owner.UpdateMoveOrder(OrderType.CastSpell, true);
                        }
                    }

                    if (Script.ScriptMetadata.AutoFaceDirection && CastInfo.Owner is not BaseTurret)
                    {
                        var goingTo = end - CastInfo.Owner.Position;

                        if (CastInfo.Targets[0].Unit != null)
                        {
                            goingTo = CastInfo.Targets[0].Unit.Position - CastInfo.Owner.Position;
                        }

                        var dirTemp = Vector2.Normalize(goingTo);
                        CastInfo.Owner.FaceDirection(new Vector3(dirTemp.X, 0, dirTemp.Y), false);
                    }
                }
            }

            if (CastInfo.IsAutoAttack && CastInfo.Owner.IsMelee)
            {
                _attackType = AttackType.ATTACK_TYPE_MELEE;
            }

            if (cast && CastInfo.Targets[0].Unit != null && CastInfo.Targets[0].Unit != CastInfo.Owner)
            {
                // Minions face their attack target via S2C_UnitSetLookAt (0x10F) only (sent on the
                // cast's LookAt point). Riot sends ZERO standalone S2C_FaceDirection (0x50) for minions
                // (replay a6db3774 diff: Riot 0x50/minion = 0; ours was ~11/minion = one redundant
                // facing packet per auto-attack on top of the LookAt). Suppress it for minions to match
                // Riot's facing wire; champions/others keep FaceDirection.
                if (Script.ScriptMetadata.AutoFaceDirection && CastInfo.Owner is not BaseTurret
                    && CastInfo.Owner is not Minion)
                {
                    ApiFunctionManager.FaceDirection(CastInfo.Targets[0].Unit.Position, CastInfo.Owner);
                }
            }

            if (CastInfo.IsAutoAttack || CastInfo.UseAttackCastTime)
            {
                // We assume it is already an attack.
                int index = CastInfo.SpellSlot - 64;
                if (CastInfo.SpellSlot >= 45 && CastInfo.SpellSlot <= 60)
                {
                    // Extra Spells which UseAttackCastTime just use the base auto attack's cast time.
                    index = 0;
                }

                // Replay-verified (TalonNoxianDiplomacyAttack CastSpellAns, 56 casts): both the
                // windup and the total scale with the LIVE attack-speed multiplier — the ratio
                // ct/tt stays exactly (AttackDelayCastPercent + AttackDelayCastOffsetPercent)
                // (0.2005 for Talon) while tt = baseCycle / attackSpeedMod (measured
                // 1.533 / 1.20..1.76). Unscaled values made the client animate empowered-AA
                // cast frames too slowly at high attack speed.
                float attackSpeedMod = Math.Max(0.0001f, CastInfo.AttackSpeedModifier);
                float autoAttackTotalTime = GlobalData.GlobalCharacterDataConstants.AttackDelay * (1.0f + CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent) / attackSpeedMod;
                // Windup = client Spell::ComputeCharacterAttackCastDelay: a Lerp between the unscaled
                // and AS-scaled cast time governed by the per-slot AttackSpeedRatio. ratio=1 (Talon,
                // Jinx — replay-verified) collapses to autoAttackTotalTime * castPercent (the old
                // formula, unchanged); ratio<1 (Thresh, Kalista) keeps the windup partially unscaled
                // so it doesn't shrink too far at high attack speed.
                CastInfo.DesignerCastTime = SpellData.GetCharacterAttackCastDelay(
                    attackSpeedMod,
                    CastInfo.Owner.CharData.BasicAttacks[index].AttackDelayOffsetPercent,
                    CastInfo.Owner.CharData.BasicAttacks[index].AttackDelayCastOffsetPercent,
                    CastInfo.Owner.CharData.BasicAttacks[index].AttackDelayCastOffsetPercentAttackSpeedRatio,
                    CastInfo.Owner.MaxAttackSpeedOverride,
                    CastInfo.Owner.MinAttackSpeedOverride);

                // TODO: Verify if this should be affected by cast variable.
                if (CastInfo.IsAutoAttack && IsBasicAttackSlotSpell())
                {
                    // Minions/pets auto-attack AUTONOMOUSLY on the client. Once a Basic_Attack puts them
                    // into the hardcode-attack state, obj_AI_Minion::UpdatePimpl (mac decomp 4.17,
                    // AIMinionClient.cpp:138) keeps re-firing the swing at the client's own computed cadence
                    // with NO further packets. So Riot sends a minion/pet a Basic_Attack only on target
                    // ACQUISITION/CHANGE, never per swing (replay 26ec2d65: Tibbers = 7 Basic_Attacks but 19
                    // hit/damage events, target NetID changing each packet). Sending one per swing made the
                    // client render OUR packet-driven attack PLUS its own autonomous one → the hit FX fired
                    // twice (the "double AA hit FX on pets" bug, reproduces even stationary).
                    // !IsSecondAutoAttack (== !HasMadeInitialAttack, which resets on (re)target) is exactly
                    // the first attack against a freshly acquired target = our "target changed" edge.
                    // Champions (obj_AI_Hero) have NO autonomous attack loop, so they still need a packet
                    // every swing: the position-less Basic_Attack (0x0C) chain for follow-ups.
                    if (!CastInfo.IsSecondAutoAttack)
                    {
                        _game.PacketNotifier.NotifyBasic_Attack_Pos(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit);
                    }
                    else if (CastInfo.Owner is not Minion)
                    {
                        _game.PacketNotifier.NotifyBasic_Attack(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit, CastInfo.Owner.HasMadeInitialAttack);
                    }
                    // else: minion/pet continuation swing on the same target — send nothing; the client's
                    // autonomous minion attack loop renders it (sending here would double the hit FX).
                }

                // Riot wire: DesignerTotalTime of EVERY attack-timed cast = the full
                // (AS-scaled) attack cycle, never ct + channel. Verified twice:
                // TalonNoxianDiplomacyAttack (56 casts, tt = baseCycle/ASmod) and replay
                // 630b7ceb (JinxQAttack family: tt*ASmod constant 1.600 across all AS
                // values). The old "UseAttackCastTime ? ct + trueChannel" branch shadowed
                // this for all attacks since UseAttackCastTime is set alongside
                // IsAutoAttack — sending windup-only totals.
                CastInfo.DesignerTotalTime = autoAttackTotalTime + trueChannel;
            }

            if (cast && CastInfo.Targets[0].Unit != null && CastInfo.Targets[0].Unit != CastInfo.Owner)
            {
                _game.PacketNotifier.NotifyS2C_UnitSetLookAt(CastInfo.Owner, CastInfo.Targets[0].Unit, _attackType);
            }

            if (cast)
            {
                if (!CastInfo.IsAutoAttack)
                {
                    Vector3 originalTarget = CastInfo.TargetPosition;
                    Vector3 originalTargetEnd = CastInfo.TargetPositionEnd;

                    if (!Script.ScriptMetadata.AutoFaceDirection && !Script.ScriptMetadata.OverrideTargetPositionInScript)
                    {
                        var owner = CastInfo.Owner;
                        // Guard against Vector2.Normalize(Vector2.Zero) returning (NaN,NaN)
                        // (see matching guard above at the cast-start fakePos site).
                        var dirXZ = new Vector2(owner.Direction.X, owner.Direction.Z);
                        var forwardVec = dirXZ == Vector2.Zero ? new Vector2(1, 0) : Vector2.Normalize(dirXZ);

                        var fakePos = owner.Position + (forwardVec * 500.0f);
                        var fakePos3D = new Vector3(fakePos.X, _game.Map.NavigationGrid.GetHeightAtLocation(fakePos), fakePos.Y);

                        CastInfo.TargetPosition = fakePos3D;
                        CastInfo.TargetPositionEnd = fakePos3D;
                    }
                    _game.PacketNotifier.NotifyNPC_CastSpellAns(this, isContinuationFromExistingCast: isContinuation);
                    if (SpellData.CanMoveWhileChanneling && !CastInfo.Owner.IsPathEnded())
                    {
                        _game.PacketNotifier.NotifyWaypointGroup(CastInfo.Owner);
                    }
                }

                // Auto-attack overrides in non-basic slots still need a normal cast packet for animation.
                if (CastInfo.IsAutoAttack && !IsBasicAttackSlotSpell())
                {
                    _game.PacketNotifier.NotifyNPC_CastSpellAns(this, isContinuationFromExistingCast: isContinuation);
                }

                // [WINDUP-DIAG] A spell whose data/script demands a windup (OverrideCastTime
                // or ScriptMetadata.CastTime) but that is about to fire instantly. Captures
                // the full decision state so the "Jinx W sometimes fires with no cast time
                // after a few autoattacks" repro can be pinned. Remove once diagnosed.
                bool wantsWindup = SpellData.OverrideCastTime > 0 || Script.ScriptMetadata.CastTime > 0;
                bool willBeInstant = CastInfo.DesignerCastTime <= 0
                    || (!CastInfo.UseAttackCastDelay && SpellData.Flags.HasFlag(SpellDataFlags.InstantCast));
                if (wantsWindup && willBeInstant)
                {
                    _logger.Debug($"[WINDUP-DIAG] {SpellName} fires INSTANT despite wanting windup: " +
                        $"DesignerCastTime={CastInfo.DesignerCastTime:F3} OverrideCastTime={SpellData.OverrideCastTime:F3} " +
                        $"ScriptCastTime={Script.ScriptMetadata.CastTime:F3} IsAutoAttack={CastInfo.IsAutoAttack} " +
                        $"UseAttackCastTime={CastInfo.UseAttackCastTime} UseAttackCastDelay={CastInfo.UseAttackCastDelay} " +
                        $"InstantCastFlag={SpellData.Flags.HasFlag(SpellDataFlags.InstantCast)} State={State} slot={CastInfo.SpellSlot} ASmod={CastInfo.AttackSpeedModifier:F3}");
                }

                if (CastInfo.DesignerCastTime > 0)
                {
                    if (Script.ScriptMetadata.TriggersSpellCasts)
                    {
                        ApiEventManager.OnSpellCast.Publish(this);
                    }

                    if (CastInfo.IsAutoAttack)
                    {
                        ApiEventManager.OnPreAttack.Publish(CastInfo.Owner, this);
                    }

                    if (!CastInfo.UseAttackCastDelay)
                    {
                        if (!SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                        {
                            State = SpellState.STATE_CASTING;
                        }
                        else
                        {
                            FinishCasting();
                        }
                    }
                    else
                    {
                        State = SpellState.STATE_CASTING;
                    }
                    CurrentCastTime = CastInfo.DesignerCastTime;
                }
                else
                {
                    FinishCasting();
                    if (GetEffectiveChannelDuration() > 0)
                    {
                        Channel();
                    }
                }
            }
            else
            {
                if (Script.ScriptMetadata.MissileParameters != null)
                {
                    CreateSpellMissile();
                }
            }

            return true;
        }

        // True while Channel() has applied the engine channel/charge action-lock (CanCast/CanAttack
        // always, CanMove when !CanMoveWhileChanneling). Guards ReleaseChannelStatusLock so the
        // ref-counted disable-holds are released exactly once, on whichever channel-end path fires.
        private bool _channelStatusLockApplied;

        // Releases the holds applied in Channel(). Idempotent (guarded), so it can be called from
        // every channel-end path (StopChanneling / FinishChanneling / ExpireCharge) without
        // double-releasing. CanMove is only released when it was held (mirror the apply condition).
        private void ReleaseChannelStatusLock()
        {
            if (!_channelStatusLockApplied)
            {
                return;
            }
            _channelStatusLockApplied = false;
            // _channelStatusLockApplied is set ONLY when Channel() actually applied the holds
            // (charge OR CantCancelWhileChanneling), so release exactly what was taken — keeps the
            // ref-counted disable-holds balanced regardless of why they were applied.
            CastInfo.Owner.SetStatus(StatusFlags.CanCast, true);
            CastInfo.Owner.SetStatus(StatusFlags.CanAttack, true);
            if (!SpellData.CanMoveWhileChanneling)
            {
                CastInfo.Owner.SetStatus(StatusFlags.CanMove, true);
            }
        }

        public void Channel()
        {
            State = SpellState.STATE_CHANNELING;

            // A STALE pre-cast attack order must not survive into the channel: ChannelCancelCheck
            // treats the Attack* order group as attack INPUT and would cancel the channel on its
            // first tick (repro: autoattack, then cast Sion Q -> charge instantly cancelled). The
            // cast consumed the order — replace it with CastSpell, exactly what the
            // !CanMoveWhileChanneling path already did in Cast(); for CanMoveWhileChanneling
            // spells nothing else clears it. A NEW attack click during the channel still cancels
            // (it arrives after this and re-sets the order).
            var staleOrder = CastInfo.Owner.MoveOrder;
            if (staleOrder == OrderType.AttackTo
                || staleOrder == OrderType.AttackTerrainOnce
                || staleOrder == OrderType.AttackTerrainSustained
                || staleOrder == OrderType.PetHardAttack)
            {
                CastInfo.Owner.UpdateMoveOrder(OrderType.CastSpell, true);
            }

            // Server-side timer uses MAX HOLD duration (>= bar-fill duration). For charge spells
            // like Varus Q this is 4s (= ChargeMaxHoldDuration), while the wire DesignerTotalTime
            // stays at 1.5s (= ChargeDuration, drives client bar fill).
            CurrentChannelDuration = GetMaxHoldDuration();

            if (CurrentChannelDuration > 0)
            {
                CastInfo.Owner.SetChannelSpell(this);
            }

            // Engine channel/charge action-lock, applied from SpellData. Capability bits are
            // ref-counted disable-holds, so this adds its OWN hold (released in
            // ReleaseChannelStatusLock on every channel-end path) and never clobbers concurrent CC
            // like a stun.
            //
            // The full action-lock (CanCast/CanAttack off, and CanMove off when !CanMoveWhileChanneling)
            // applies ONLY when the player is COMMITTED for the duration:
            //   - CHARGE spells: the recast arrives via UpdateCharge (not the CanCast-gated cast pipeline),
            //     so other casts/attacks stay blocked.
            //   - UNCANCELLABLE channels (CantCancelWhileChanneling=1, e.g. Pantheon E): fully locked, the
            //     channel runs its whole duration and the client must not predict movement/cast.
            // A CANCELLABLE channel (CantCancelWhileChanneling=0, e.g. Katarina R, Yi Meditate) takes NO
            // capability holds: it is cancelled BY a cast/attack/move INPUT (ChannelCancelCheck), so those
            // inputs must be allowed to register — a CanMove hold would make CanIssueMoveOrders()
            // (ObjAIBase.cs:726) reject the move and the channel could never be move-cancelled. Its no-move
            // is still enforced engine-side by CanMove()'s ChannelSpell clause (ObjAIBase.cs:671), so it
            // only needs StopMovement(). Verified vs S1 lua: NO channel script self-locks movement — the
            // no-move + commit behavior is purely data-driven (CanMoveWhileChanneling / CantCancelWhileChanneling).
            if (IsChargeSpell || SpellData.CantCancelWhileChanneling)
            {
                CastInfo.Owner.SetStatus(StatusFlags.CanCast, false);
                CastInfo.Owner.SetStatus(StatusFlags.CanAttack, false);
                if (!SpellData.CanMoveWhileChanneling)
                {
                    CastInfo.Owner.StopMovement();
                    CastInfo.Owner.SetStatus(StatusFlags.CanMove, false);
                }
                _channelStatusLockApplied = true;
            }
            else if (!SpellData.CanMoveWhileChanneling)
            {
                CastInfo.Owner.StopMovement();
            }

            // Route to charge or channel events. IsChargeSpell covers JSON UseChargeChanneling=1
            // AND script-side ScriptMetadata.ChargeDuration > 0.
            if (IsChargeSpell)
            {
                ApiEventManager.OnSpellChargeStart.Publish(this);
                if (State == SpellState.STATE_CHANNELING)
                {
                    ApiEventManager.OnSpellChargeTick.Publish(this, 0f);
                }
            }
            else
            {
                ApiEventManager.OnSpellChannel.Publish(this);
                // Channel-entry tick: diff=0f marks "no time elapsed since channel start".
                // Scripts using PeriodicTicker should pass fireImmediately=true if they want to
                // act on this initial call; otherwise their accumulator stays at 0 until the first
                // real Update() with non-zero diff arrives next server tick.
                if (State == SpellState.STATE_CHANNELING)
                {
                    ApiEventManager.OnSpellChannelUpdate.Publish(this, 0f);
                }
            }
        }

        public void ChannelCancelCheck()
        {
            if (CastInfo.Owner.IsDead)
            {
                CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.Die);
                return;
            }

            // TODO: Verify if this should only be checked at the start of channeling.
            if (!SpellData.CanMoveWhileChanneling && CastInfo.Owner.MovementParameters != null)
            {
                CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.Move);
                return;
            }

            // M2 Phase 3: a channel is a cast — cast-disabling CC (charm/fear/silence/stun/suppress/taunt)
            // clears the CanCast capability (BuffType.ToCapabilityDisable). Query the CC BUFFS directly via
            // IsUnderCastDisablingCC, NOT the raw !CanCast flag: Channel() imperatively clears CanCast as its
            // OWN action-lock (Spell.cs Channel(): SetStatus(CanCast,false)), so checking the flag would make
            // every channel self-cancel on its first tick (StunnedOrSilencedOrTaunted) instantly.
            if (CastInfo.Owner.IsUnderCastDisablingCC)
            {
                CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.StunnedOrSilencedOrTaunted);
                return;
            }
            if (SpellData.TargetingType == TargetingType.Target)
            {
                if (CastInfo.Targets.Count <= 0 || CastInfo.Targets[0].Unit == null)
                {
                    CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
                    return;
                }

            }
            // Uncancellable
            if (SpellData.CantCancelWhileChanneling)
            {
                return;
            }

            if (CastInfo.Targets.Count > 0 && CastInfo.Targets[0].Unit != null)
            {
                var spellTarget = CastInfo.Targets[0].Unit;

                if (spellTarget != null && (!spellTarget.IsVisibleByTeam(CastInfo.Owner.Team) || (!spellTarget.Status.HasFlag(StatusFlags.Targetable) && !spellTarget.CharData.IsUseable) || spellTarget.IsDead))



                {
                    CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
                    return;
                }
            }

            // TODO: ChannelingStopSource.HeroReincarnate

            var order = CastInfo.Owner.MoveOrder;
            if (!SpellData.CanMoveWhileChanneling
            && (order == OrderType.MoveTo
                || order == OrderType.AttackMove
                || order == OrderType.PetHardMove))
            {
                if (order == OrderType.MoveTo
                || order == OrderType.AttackMove
                || order == OrderType.PetHardMove)
                {
                    CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.Move);
                    return;
                }
            }

            if (order == OrderType.AttackTo
            || order == OrderType.AttackTerrainOnce
            || order == OrderType.AttackTerrainSustained
            || order == OrderType.PetHardAttack)
            {
                CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.Attack);
                return;
            }

            var castSpell = CastInfo.Owner.GetCastSpell();
            if (castSpell != null
            && !castSpell.SpellData.DoesntBreakChannels
            && order == OrderType.CastSpell)
            {
                CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.Casting);
                return;
            }

            // TODO: ChannelingStopSource.Unknown
        }

        public void StopChanneling(ChannelingStopCondition condition, ChannelingStopSource reason)
        {
            ReleaseChannelStatusLock();
            if (condition == ChannelingStopCondition.Cancel)
            {
                // S4 replay: NPC_InstantStop_Attack (0x34) is the wire signal for channel-cancel
                // (verified on Recall: cancelled channel emits it, naturally completed channel does NOT).
                // Skip on TimeCompleted so natural completion stays silent on the wire.
                if (reason != ChannelingStopSource.TimeCompleted)
                {
                    _game.PacketNotifier.NotifyNPC_InstantStop_Attack(CastInfo.Owner, false);
                    // Route to charge-specific cancel for charge spells.
                    if (IsChargeSpell)
                    {
                        ApiEventManager.OnSpellChargeCancel.Publish(this, reason);
                    }
                    else
                    {
                        ApiEventManager.OnSpellChannelCancel.Publish(this, reason);
                    }
                }

                if (CastInfo.Owner.ChannelSpell == this)
                {
                    CastInfo.Owner.SetChannelSpell(null);
                }

                var status = CastInfo.Owner.Status;
                bool isForcedMovement = status.HasFlag(StatusFlags.Feared) ||
                                        status.HasFlag(StatusFlags.Charmed) ||
                                        status.HasFlag(StatusFlags.Taunted);

                if (reason != ChannelingStopSource.Move && reason != ChannelingStopSource.Attack &&
                    !isForcedMovement)
                {
                    if (!SpellData.CanMoveWhileChanneling)
                    {
                        if (!CastInfo.Owner.IsPathEnded())
                        {
                            CastInfo.Owner.UpdateMoveOrder(OrderType.MoveTo, true);
                            ApiFunctionManager.NotifyWaypointGroup(CastInfo.Owner);
                        }
                        else
                        {
                            CastInfo.Owner.UpdateMoveOrder(OrderType.Hold, true);
                        }
                    }
                }
                // TODO: Find out how League calculates cooldown reduction for incomplete channels (assuming it isn't done in-script).
            }

            State = SpellState.STATE_READY;

            if (reason == ChannelingStopSource.TimeCompleted)
            {
                FinishChanneling();
            }
        }

        public void FinishCasting()
        {
            if (CastInfo.IsAutoAttack)
            {
                ApiEventManager.OnLaunchAttack.Publish(CastInfo.Owner, this);
            }

            if (CastInfo.IsAutoAttack || CastInfo.UseAttackCastTime)
            {
                CastInfo.Owner.HasAutoAttacked = true;
                bool shouldAdvanceBasicAttackChain = CastInfo.IsAutoAttack && IsBasicAttackSlotSpell();
                if (shouldAdvanceBasicAttackChain && !CastInfo.Owner.HasMadeInitialAttack)
                {
                    CastInfo.Owner.HasMadeInitialAttack = true;
                }

                // (Former attack winddown chase-lock removed 2026-06-25: per the AA-speed wiki the WINDDOWN
                // is FREE movement — only the WINDUP locks the unit. Holding the unit still for the whole
                // recovery delayed the chase by ~the remaining cooldown, so the swing visibly "played
                // through" while the unit should already be running. The post-windup re-chase no longer
                // slides because we now send the client an NPC_InstantStop_Attack the moment it disengages
                // to chase, see ObjAIBase.UpdateTarget — that stops the client's autonomous swing loop, so
                // a server-side hold is unnecessary.)
                // Note: `CharData.PostAttackMoveDelay` is loaded for forward-compat but NOT
                // wired here — verified 2026-05-10 that S4 client doesn't consume this field
                // (literal "PostAttack" doesn't appear in S4 decomp; CharacterData reader uses
                // literal-string ReadCFG_S/I/B which would surface any consumed field). Wiring
                // it would create server-only behavior the client doesn't model. See memory
                // `project_chardata_chasing_postattack_loaded.md` for the verification trail.
                // `ObjAIBase.EngagePostAttackMoveLock` exists as plumbing for future spells/
                // buffs that may want a similar lockout, but is not auto-invoked here.
                if (!CastInfo.Owner.IsMelee)
                {
                    if (HasEmptyScript)
                    {
                        CreateSpellMissile(new MissileParameters
                        {
                            Type = MissileType.Target
                        });
                    }
                }
                else
                {
                    ApplyEffects(CastInfo.Targets[0].Unit);
                    CastInfo.Owner.AutoAttackHit(CastInfo.Targets[0].Unit);
                }

                State = SpellState.STATE_READY;
            }
            else
            {
                if (GetEffectiveChannelDuration() <= 0)
                {
                    State = SpellState.STATE_COOLDOWN;

                    CurrentCooldown = GetCooldown();
                    SetPassiveCooldownStats(CurrentCooldown);

                    // Champion ability slots (Q/W/E/R = 0..3) derive their cooldown client-side from CastSpellAns.
                    // S4 replay shows zero CHAR_SetCooldown packets for these slots across a full match;
                    // sending it caused a visible double-cooldown flicker on instant-cast targeted spells (Katarina E).
                    // Manual cooldown changes (CDR procs, refunds, item resets) still flow through SetCooldown(...) below.
                    if (CastInfo.SpellSlot >= 4)
                    {
                        _game.PacketNotifier.NotifyCHAR_SetCooldown(CastInfo.Owner, CastInfo.SpellSlot, CurrentCooldown, GetCooldown());
                    }
                }
            }

            if (Script.ScriptMetadata.MissileParameters != null)
            {
                CreateSpellMissile();
            }

            // Show the spell's popup message (localization key) as floating text over the caster
            // (NPC_MessageToClient via Say) when the cast completes, if the script set one. No sayTo =>
            // broadcast to everyone.
            if (!string.IsNullOrEmpty(Script.ScriptMetadata.PopupMessage1))
            {
                ApiFunctionManager.Say(CastInfo.Owner, Script.ScriptMetadata.PopupMessage1);
            }

            bool shouldResumeWalking = !CastInfo.Owner.IsPathEnded();

            if (CastInfo.Owner.GetCastSpell() == this)
            {
                CastInfo.Owner.SetCastSpell(null);
            }

            if (CastInfo.Owner.SpellToCast != null && CastInfo.Owner.SpellToCast == this)
            {
                CastInfo.Owner.SetSpellToCast(null, Vector2.Zero);
            }
            if (Script.ScriptMetadata.TriggersSpellCasts)
            {
                ApiEventManager.OnSpellPostCast.Publish(this);
            }
            if (CastInfo.Owner.GetCastSpell() != null)
            {
                return;
            }
            if (CastInfo.Owner.MovementParameters == null)
            {
                if (SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                {
                    if (shouldResumeWalking)
                    {
                        CastInfo.Owner.UpdateMoveOrder(OrderType.MoveTo, true);
                        ApiFunctionManager.NotifyWaypointGroup(CastInfo.Owner);
                    }
                    else if (CastInfo.Owner.TargetUnit != null && CastInfo.Owner.MoveOrder != OrderType.Hold)
                    {
                        // Preserve a player Hold (H): after an auto-attack while holding, the unit must
                        // stay in Hold (stand + attack in range), NOT be promoted to AttackTo (chase).
                        CastInfo.Owner.UpdateMoveOrder(OrderType.AttackTo, true);
                    }
                }
                else
                {
                    bool willChannel = GetEffectiveChannelDuration() > 0;

                    if (!willChannel)
                    {
                        if (shouldResumeWalking)
                        {
                            CastInfo.Owner.UpdateMoveOrder(OrderType.MoveTo, true);
                            ApiFunctionManager.NotifyWaypointGroup(CastInfo.Owner);
                        }
                        else if (CastInfo.Owner.TargetUnit != null && CastInfo.Owner.MoveOrder != OrderType.Hold)
                        {
                            // After an auto-attack (or any targeted, non-walking cast) keep the
                            // order as AttackTo so the unit re-engages/chases the target when it
                            // leaves range — mirroring the InstantCast branch above and Riot's
                            // persistent AI_HARDATTACK. Previously this fell through to Hold, which
                            // (with the Hold-aware auto-promote guard in RefreshWaypoints) trapped the
                            // unit in Hold after the first in-range attack so it never chased again.
                            // EXCEPT a player Hold (H): keep Hold so a held unit attacks in place but
                            // never chases (the H = "don't move but fight" command).
                            CastInfo.Owner.UpdateMoveOrder(OrderType.AttackTo, true);
                        }
                        else
                        {
                            CastInfo.Owner.UpdateMoveOrder(OrderType.Hold, true);
                        }
                    }
                    else
                    {
                        if (!SpellData.CanMoveWhileChanneling)
                        {
                            CastInfo.Owner.StopMovement();
                            if (!shouldResumeWalking)
                            {
                                CastInfo.Owner.UpdateMoveOrder(OrderType.Hold, true);
                            }
                        }
                    }
                }
            }
        }


        public void FinishChanneling()
        {
            ReleaseChannelStatusLock();
            State = SpellState.STATE_COOLDOWN;

            if (CastInfo.Owner.ChannelSpell == this)
            {
                CastInfo.Owner.SetChannelSpell(null);
            }

            bool shouldResumeWalking = !CastInfo.Owner.IsPathEnded();

            // Route to charge-specific fire event for UseChargeChanneling spells.
            // For charge-spells reaching FinishChanneling = the charge completed (either by
            // PlayerCommand release routed through UpdateCharge → StopChanneling(Success,
            // TimeCompleted), or by max charge time elapsing). Both fire the missile.
            if (IsChargeSpell)
            {
                ApiEventManager.OnSpellChargeFire.Publish(this);
            }
            else
            {
                ApiEventManager.OnSpellPostChannel.Publish(this);
            }
            if (CastInfo.Owner.MovementParameters == null)
            {
                if (shouldResumeWalking)
                {
                    CastInfo.Owner.UpdateMoveOrder(OrderType.MoveTo, true);
                    ApiFunctionManager.NotifyWaypointGroup(CastInfo.Owner);
                }
                else
                {
                    CastInfo.Owner.UpdateMoveOrder(OrderType.Hold, true);
                    // Intentionally NO NotifyWaypointGroup here — replay (KatarinaR @ t=578760+)
                    // shows ONLY StopAnimation + BuffRemove2 for the caster after channel end,
                    // no WaypointGroup. A path-sync with the channel-lock position as start
                    // would snap the client back if it already predicted forward from the click.
                }
            }

            CurrentCooldown = GetCooldown();
            SetPassiveCooldownStats(CurrentCooldown);

            // See FinishCasting: ability slots 0..3 are client-driven from CastSpellAns; only summoner+item slots need this.
            if (CastInfo.SpellSlot >= 4)
            {
                _game.PacketNotifier.NotifyCHAR_SetCooldown(CastInfo.Owner, CastInfo.SpellSlot, CurrentCooldown, GetCooldown());
            }
        }

        /// <summary>
        /// Charge timeout — the player held past <see cref="GetMaxHoldDuration"/> without releasing
        /// (or the bar filled with no MaxHoldDuration override). Publishes
        /// <see cref="ApiEventManager.OnSpellChargeCancel"/> with
        /// <c>ChannelingStopSource.TimeCompleted</c> so the script can decide expire policy
        /// (refund mana, auto-fire, etc.). Sets cooldown like <see cref="FinishChanneling"/> —
        /// the spell DID consume mana, even if the script refunds some of it via the event hook.
        ///
        /// <para>Charge-pipeline only — non-charge spells should use <see cref="FinishChanneling"/>
        /// directly via <c>StopChanneling(Success, TimeCompleted)</c>.</para>
        /// </summary>
        public void ExpireCharge()
        {
            ReleaseChannelStatusLock();
            State = SpellState.STATE_COOLDOWN;

            if (CastInfo.Owner.ChannelSpell == this)
            {
                CastInfo.Owner.SetChannelSpell(null);
            }

            bool shouldResumeWalking = !CastInfo.Owner.IsPathEnded();

            // Wire-side cleanup signal: NPC_InstantStop_Attack tells the client to exit charge state
            // (mSpellCasted=0, charge bar clears, spell becomes recastable). Without this the client's
            // SpellInstanceClient stays at mChannelingFinished=0 + mChannelingFinishTime=FLT_MAX so
            // IsChanneling() returns true forever and Spellbook refuses to re-cast Q. Same mechanism
            // as StopChanneling(Cancel, non-TimeCompleted) uses for stun/silence/etc cancels.
            _game.PacketNotifier.NotifyNPC_InstantStop_Attack(CastInfo.Owner, false);

            // Publish CANCEL (not Fire) — script chooses policy: mana refund, auto-fire, etc.
            ApiEventManager.OnSpellChargeCancel.Publish(this, ChannelingStopSource.TimeCompleted);

            if (CastInfo.Owner.MovementParameters == null)
            {
                if (shouldResumeWalking)
                {
                    CastInfo.Owner.UpdateMoveOrder(OrderType.MoveTo, true);
                    ApiFunctionManager.NotifyWaypointGroup(CastInfo.Owner);
                }
                else
                {
                    CastInfo.Owner.UpdateMoveOrder(OrderType.Hold, true);
                }
            }

            CurrentCooldown = GetCooldown();
            SetPassiveCooldownStats(CurrentCooldown);

            if (CastInfo.SpellSlot >= 4)
            {
                _game.PacketNotifier.NotifyCHAR_SetCooldown(CastInfo.Owner, CastInfo.SpellSlot, CurrentCooldown, GetCooldown());
            }
        }

        public void Deactivate()
        {
            CastInfo.Targets.Clear();
            ResetSpellCast();
            SetSpellToggle(false);
            if (CastInfo.Owner.GetCastSpell() == this)
            {
                CastInfo.Owner.SetCastSpell(null);
            }
            if (CastInfo.Owner.ChannelSpell == this)
            {
                CastInfo.Owner.SetChannelSpell(null);
            }
            if (CastInfo.Owner.SpellToCast == this)
            {
                CastInfo.Owner.SetSpellToCast(null, Vector2.Zero);
            }

            try
            {
                using var _scope = Profiler.Scope($"spell:{CastInfo?.Owner?.Model ?? "?"}/{SpellName}.OnDeactivate", "scripts");
                Script.OnDeactivate(CastInfo.Owner, this);
            }
            catch(Exception e)
            {
                _logger.Error(null, e);
            }
        }

        /// <summary>
        /// Creates a spell missile with the given parameters.
        /// </summary>
        /// <param name="parameters">Parameters of the missile.</param>
        public SpellMissile CreateSpellMissile(MissileParameters parameters)
        {
            if (CastInfo.MissileNetID == 0)
            {
                return null;
            }

            var netId = CastInfo.MissileNetID;

            if (_game.ObjectManager.GetObjectById(netId) != null)
            {
                netId = _game.NetworkIdManager.GetNewNetId();
            }

            bool isServerOnly = SpellData.MissileEffect != "";

            SpellMissile p = null;
            var castInfoClone = CastInfo.Clone();
            // The packet's inner MissileNetID must match the actual missile NetId — Riot's
            // wire holds outer SenderNetID == inner CastInfo.MissileNetID == missile id in
            // 2179/2179 MissileReplications across two replays. When the collision branch
            // above assigned a fresh netId, the clone still carried the old one.
            // CreateCustomMissile already does this; mirror it here.
            castInfoClone.MissileNetID = netId;

            // Forensic-verified (memory: MissileReplication wire shape §"How
            // MissileTargetHeightAugment propagates"): the clone's launch Y carries the
            // height augment ("bow level"); TargetPosition/-End stay at ground level. The
            // packet builder copies this launch Y into ALL outer Y fields — without it the
            // client renders the missile inside the terrain.
            var launchPos = castInfoClone.SpellCastLaunchPosition;
            float effectiveHeightAugment = parameters.OverrideHeightAugment ?? SpellData.MissileTargetHeightAugment;
            castInfoClone.SpellCastLaunchPosition = new Vector3(
                launchPos.X, launchPos.Y + effectiveHeightAugment, launchPos.Z);

            // Script-side CollisionRadius override falls back to SpellData.LineWidth when null.
            // Useful for missiles whose visible hit area doesn't match the JSON LineWidth
            // (e.g. Diana W orbs have LineWidth=0 in JSON but need a 150u hit radius to match
            // the visible orb size — without this override, orbs phase through enemies).
            int collisionRadius = parameters.CollisionRadius ?? (int)SpellData.LineWidth;

            // MissileFixedTravelTime (JSON): lob missiles fly with a FIXED travel time, so the
            // speed scales with distance — speed = dist / time. Replay-verified on JinxEHit
            // (MissileFixedTravelTime=0.4): 126 MISREPs with dist/v = 0.368..0.414s and wire
            // speeds 364..2283, NOT clamped to MissileMin-/MaxSpeed. Must be computed BEFORE
            // construction so the server simulation and the spawn MissileReplication (sent
            // inline by AddObject) carry the same velocity — a post-spawn SetSpeed only fixes
            // the server side and desyncs the client visual.
            float missileSpeed = SpellData.MissileSpeed;
            if (SpellData.MissileFixedTravelTime > 0)
            {
                var launch2D = new Vector2(castInfoClone.SpellCastLaunchPosition.X, castInfoClone.SpellCastLaunchPosition.Z);
                Vector2 end2D;
                if (parameters.OverrideEndPosition != default)
                {
                    end2D = parameters.OverrideEndPosition;
                }
                else if (castInfoClone.Targets.Count > 0 && castInfoClone.Targets[0].Unit != null)
                {
                    end2D = castInfoClone.Targets[0].Unit.Position;
                }
                else
                {
                    end2D = new Vector2(castInfoClone.TargetPositionEnd.X, castInfoClone.TargetPositionEnd.Z);
                }
                missileSpeed = MathF.Max(1f, Vector2.Distance(launch2D, end2D) / SpellData.MissileFixedTravelTime);
            }

            switch (parameters.Type)
            {
                case MissileType.Target:
                    {
                        p = new SpellMissile(
                            _game,
                            collisionRadius,
                            this,
                            castInfoClone,
                            missileSpeed,
                            netId,
                            isServerOnly
                        );
                        break;
                    }
                case MissileType.Chained:
                    {
                        p = new SpellChainMissile(
                            _game,
                            collisionRadius,
                            this,
                            castInfoClone,
                            parameters,
                            missileSpeed,
                            netId,
                            isServerOnly
                        );
                        break;
                    }
                // Canonical name→class mapping (same as CreateCustomMissile):
                // Circle = SpellCircleMissile (orbit, e.g. Diana W orbs),
                // Arc = SpellLineMissile (straight line + optional unit tracking).
                case MissileType.Circle:
                    {
                        p = new SpellCircleMissile(
                            _game,
                            collisionRadius,
                            this,
                            castInfoClone,
                            missileSpeed,
                            parameters.OverrideEndPosition,
                            netId,
                            isServerOnly
                        );
                        break;
                    }
                case MissileType.Arc:
                    {
                        p = new SpellLineMissile(
                            _game,
                            collisionRadius,
                            this,
                            castInfoClone,
                            missileSpeed,
                            parameters.OverrideEndPosition,
                            netId,
                            isServerOnly
                        );
                        break;
                    }
            }

            // If the position is the same as the destination, the server will have destroyed the missile before notifying of creation, causing the client to crash.
            // TODO: Make a better check.
            if (p == null || (p.HasDestination() && p.Position == p.Destination)
                || p.Position == p.GetTargetPosition())
            {
                return null;
            }

            // Sub-missiles (TriggersSpellCasts=false, e.g. KatarinaRMis dagger ticks,
            // KatarinaQMis pre-bounce flow) need MissileReplication for client visibility this is
            // replay-verified (KatarinaR id=66753: 0 CastSpellAns + 10 MissileReplication for
            // the 10 RMis daggers). Default HasClientCastInfo=true would skip MissileReplication
            // in ConstructSpawnPacket and the missile would never appear client-side. AA missiles
            // keep default true (Basic_Attack_Pos serves as cast info).
            //
            // MUST be set before AddObject because the ObjectManager.AddObject calls SpawnObject() inline
            // (ObjectManager.cs:254-256) which immediately runs visibility-spawn → ConstructSpawnPacket.
            // Setting HasClientCastInfo after AddObject is too late; the spawn packet has already
            // been chosen based on the default (true), causing sub-missiles to skip MissileReplication.
            if (!Script.ScriptMetadata.TriggersSpellCasts && !CastInfo.IsAutoAttack)
            {
                p.HasClientCastInfo = false;
            }

            // Must be set before AddObject — the spawn MissileReplication is constructed
            // inline there and reads the missile's effective height augment.
            p.HeightAugmentOverride = parameters.OverrideHeightAugment;
            p.TimedSpeedDelta = parameters.TimedSpeedDelta;
            p.TimedSpeedDeltaTime = parameters.TimedSpeedDeltaTime;

            _game.ObjectManager.AddObject(p);

            ApiEventManager.OnLaunchMissile.Publish(this, p);

            // S4-verified (obj_AI_Base.cpp:21514 OnNetworkPacket<PKT_S2C_ForceCreateMissile_s>
            // → SpellInstanceClient::ForceSpawnMissile → ExecuteCastFrame, SpellInstanceClient.cpp:471):
            // the packet tells the client to spawn the missile immediately (bypassing natural
            // windup-end trigger). Replay shows ForceCreateMissile fires at +225ms after Q's
            // CastSpellAns (= cast-windup-end) and 34066/34626 packets across the match are
            // for AA missiles (98%); both flows route through CreateSpellMissile so this single
            // call site covers all cases. Bounce missiles in SpellChainMissile.BounceToNextTarget
            // intentionally bypass this — replay confirms no per-bounce ForceCreateMissile.
            //
            // Sub-missiles (HasClientCastInfo=false, e.g. Diana W orbs, KatarinaRMis daggers)
            // also skip this packet: the client's handler gates on a pending spellbook entry
            // whose missileNetworkID matches — sub-missiles have neither (parent cast already
            // completed, sub-missile NetID differs), so the packet is silently dropped client-
            // side. Riot's replay confirms 0× ForceCreateMissile for DianaOrbsMissile across
            // 40 W casts. Skip the wasted broadcast.
            if (p.HasClientCastInfo)
            {
                _game.PacketNotifier.NotifyForceCreateMissile(p);

                // A ForceCreate'd missile spawns from the client's spellbook entry at the spell's base
                // MissileSpeed; a scheduled speed boost (Jinx R: +500 after 0.75s) lives only in the
                // MissileReplication's TimedSpeedDelta fields, which this path does NOT send. So push it
                // explicitly via S2C_ChangeMissileSpeed on the SAME tick — exactly what Riot does
                // (replay: ForceCreateMissile + S2C_ChangeMissileSpeed{+500, 0.75} same tick). The
                // vision-acquire (MissileReplication) path already carries TimedSpeedDelta, so it's
                // covered there and must NOT be double-sent.
                if (p.TimedSpeedDelta != 0f)
                {
                    _game.PacketNotifier.NotifyS2C_ChangeMissileSpeed(p, p.TimedSpeedDelta, p.TimedSpeedDeltaTime);
                }
            }

            return p;
        }

        /// <summary>
        /// Creates a spell missile using this spell's script for the parameters.
        /// </summary>
        public SpellMissile CreateSpellMissile()
        {
            return CreateSpellMissile(Script.ScriptMetadata.MissileParameters);
        }


        public float GetCooldown()
        {
            if (_game.Config.GameFeatures.HasFlag(FeatureFlags.EnableCooldowns))
            {
                if (SpellData.Cooldown == null || SpellData.Cooldown.Length == 0)
                {
                    return 0.0f;
                }

                var level = Math.Clamp(CastInfo.SpellLevel, (byte)0, (byte)(SpellData.Cooldown.Length - 1));
                var cd = SpellData.Cooldown[level];

                // AutoCooldownByLevel override: when a script sets a per-rank value > 0 it
                // overrides the inibin SpellData.Cooldown for that level (default {0,...} = no
                // override -> use SpellData.Cooldown). This lets us
                // tune cooldowns per spell in the script without editing inibin data. CDR (below) still applies.
                var autoCd = Script?.ScriptMetadata?.AutoCooldownByLevel;
                if (autoCd != null && autoCd.Length > 0)
                {
                    var autoLevel = Math.Clamp(CastInfo.SpellLevel, (byte)0, (byte)(autoCd.Length - 1));
                    if (autoCd[autoLevel] > 0)
                    {
                        cd = autoCd[autoLevel];
                    }
                }

                if (Script?.ScriptMetadata?.CooldownIsAffectedByCDR == true)
                {
                    cd *= 1 + CastInfo.Owner.Stats.GetClampedCooldownReduction();
                }
                // Floor the final cooldown at gcd_CooldownMinimum (Constants.var: "Minimum cooldown time
                // for a spell", 0.0). Defensive: the CDR clamp above already prevents a negative multiplier.
                return Math.Max(cd, GlobalData.GlobalCharacterDataConstants.CooldownMinimum);
            }

            return 0.0f;
        }

        public float GetAmmoRechageTime()
        {
            if (SpellData.AmmoRechargeTime == null || SpellData.AmmoRechargeTime.Length == 0)
            {
                return 0.0f;
            }

            var spellLevel = Math.Max(1, (int)CastInfo.SpellLevel);
            var level = Math.Clamp(spellLevel - 1, 0, SpellData.AmmoRechargeTime.Length - 1);
            var cd = SpellData.AmmoRechargeTime[level];

            if (Script?.ScriptMetadata?.CooldownIsAffectedByCDR == true)
            {
                cd *= 1 + CastInfo.Owner.Stats.GetClampedCooldownReduction();
            }

            return cd;
        }

        public void AddAmmo(int ammount = 1)
        {
            CurrentAmmo = Math.Min(CurrentAmmo + ammount, SpellData.MaxAmmo);
            CurrentAmmoCooldown = GetAmmoRechageTime();


            _game.PacketNotifier.NotifyS2C_AmmoUpdate(this);
            // Riot fires HandleOnAmmoUpdate(currentAmmo, slot) on every ammo-count change.
            ApiEventManager.OnUpdateAmmo.Publish(CastInfo.Owner, this);
        }

        public float GetCurrentCastRange()
        {
            if (_overrrideCastRange > 0)
            {
                return _overrrideCastRange;
            }

            float castRange = SpellData.CastRange[0];

            if (CastInfo.SpellLevel == 0)
            {
                return castRange;
            }

            if (CastInfo.SpellLevel > 0)
            {
                for (int i = 1; i < SpellData.CastRange.Length - 1; i++)
                {
                    if (SpellData.CastRange[i] > castRange && CastInfo.SpellLevel == i)
                    {
                        castRange = SpellData.CastRange[i];
                    }
                }
            }

            return castRange;
        }

        // === Charge spell helpers (UseChargeChanneling=1) ===
        // All read from SpellData JSON fields so scripts don't hardcode constants. Replay-verified
        // for Varus Q: ChannelDuration[level]=1.25s, CastRange[level]=925, CastRangeGrowthDuration=1.3,
        // CastRangeGrowthMax=1600.

        /// <summary>
        /// True if this spell uses the charge pipeline (UseChargeChanneling=1 in JSON OR script
        /// set <see cref="SpellScriptMetadata.ChargeDuration"/> &gt; 0). Routing flag for which
        /// events fire (OnSpellCharge* vs OnSpellChannel*).
        /// </summary>
        public bool IsChargeSpell => SpellData.UseChargeChanneling
                                  || (Script != null && Script.ScriptMetadata.ChargeDuration > 0);

        /// <summary>
        /// "Channel-lock" charge-spell subcategory (Vel'Koz R, Sion R, SionRRun):
        /// button release/recast is treated as INTERRUPT (→ <c>OnSpellChargeCancel</c>),
        /// not as fire. Distinct from "tap-charge" spells like Varus Q where release =
        /// commit/fire (→ <c>OnSpellChargeFire</c>). Identified by Riot's
        /// <c>PreventChargingSecondCast = 1</c> JSON marker.
        /// </summary>
        public bool IsChannelLockCharge => IsChargeSpell && SpellData.PreventChargingSecondCast;

        /// <summary>
        /// Effective channel/charge duration in seconds, with priority:
        /// 1. <see cref="SpellScriptMetadata.ChargeDuration"/> (script-side charge override)
        /// 2. <see cref="SpellScriptMetadata.ChannelDuration"/> (legacy script-side channel override)
        /// 3. JSON <c>SpellTargeter1.RangeGrowthDuration</c> for <c>UseChargeChanneling</c> spells —
        ///    Riot routinely sets this differently from <c>ChannelDuration</c> (Varus Q:
        ///    ChannelDuration=1.25 but SpellTargeter1.RangeGrowthDuration=1.5, the latter
        ///    matches the visible bar-fill time which is how gameplay perceives "charge time")
        /// 4. <c>SpellData.ChannelDuration[level]</c> (final fallback)
        /// Used to drive both server-side <c>CurrentChannelDuration</c> ticking and the wire-side
        /// <c>DesignerTotalTime</c> sent in NPC_CastSpellAns.
        /// </summary>
        public float GetEffectiveChannelDuration()
        {
            if (Script != null && Script.ScriptMetadata.ChargeDuration > 0)
            {
                return Script.ScriptMetadata.ChargeDuration;
            }
            if (Script != null && Script.ScriptMetadata.ChannelDuration > 0)
            {
                return Script.ScriptMetadata.ChannelDuration;
            }
            if (SpellData.UseChargeChanneling && SpellData.SpellTargeters.Length > 0)
            {
                float targeterGrowth = SpellData.SpellTargeters[0].RangeGrowthDuration;
                if (targeterGrowth > 0)
                {
                    return targeterGrowth;
                }
            }
            int level = Math.Clamp(CastInfo.SpellLevel, (byte)0, (byte)(SpellData.ChannelDuration.Length - 1));
            return SpellData.ChannelDuration[level];
        }

        /// <summary>
        /// Maximum charge duration in seconds. Alias for <see cref="GetEffectiveChannelDuration"/>
        /// from a charge-spell perspective — this is the bar-fill time (when range is fully grown,
        /// charge UI is full). For server-side auto-expire timer use <see cref="GetMaxHoldDuration"/>.
        /// </summary>
        public float GetMaxChargeDuration() => GetEffectiveChannelDuration();

        /// <summary>
        /// Server-side max hold duration for charge spells. Returns <see cref="SpellScriptMetadata.ChargeMaxHoldDuration"/>
        /// if explicitly set (&gt; 0) on a charge spell, otherwise falls back to <see cref="GetEffectiveChannelDuration"/>.
        ///
        /// <para>Used by <see cref="Channel"/> to initialize the server's <see cref="CurrentChannelDuration"/>
        /// timer. May exceed the wire-side <c>DesignerTotalTime</c> (which uses <see cref="GetEffectiveChannelDuration"/>)
        /// — e.g. Varus Q: wire = 1.5s (bar fill), server hold = 4s (player can wait 2.5s past max
        /// charge before auto-expire).</para>
        /// </summary>
        public float GetMaxHoldDuration()
        {
            if (IsChargeSpell && Script != null && Script.ScriptMetadata.ChargeMaxHoldDuration > 0)
            {
                return Script.ScriptMetadata.ChargeMaxHoldDuration;
            }
            return GetEffectiveChannelDuration();
        }

        /// <summary>
        /// Elapsed charge time in seconds since charge-start. Computed as
        /// <c>MaxHoldDuration − CurrentChannelDuration</c> because the server's
        /// <see cref="CurrentChannelDuration"/> is initialized to <see cref="GetMaxHoldDuration"/>
        /// in <see cref="Channel"/>.
        ///
        /// <para>Range can exceed <see cref="GetMaxChargeDuration"/> for spells with a longer
        /// <c>ChargeMaxHoldDuration</c> than <c>ChargeDuration</c> (e.g. Varus Q: bar fills at
        /// 1.5s, MaxHold is 4s — elapsed reaches up to 4s but <see cref="GetChargeProgress"/>
        /// clamps to 1.0 after 1.5s and <see cref="GetCurrentChargeRange"/> caps at max range
        /// after <c>CastRangeGrowthDuration</c>).</para>
        /// </summary>
        public float GetChargeElapsed()
        {
            float maxHold = GetMaxHoldDuration();
            float elapsed = maxHold - CurrentChannelDuration;
            if (elapsed < 0f) elapsed = 0f;
            if (elapsed > maxHold) elapsed = maxHold;
            return elapsed;
        }

        /// <summary>
        /// Charge progress as 0..1, normalized by <see cref="GetMaxChargeDuration"/>. Useful for
        /// FX intensity, sound pitch, range-growth interpolation.
        /// </summary>
        public float GetChargeProgress()
        {
            float max = GetMaxChargeDuration();
            if (max <= 0f) return 0f;
            return Math.Clamp(GetChargeElapsed() / max, 0f, 1f);
        }

        /// <summary>
        /// Maximum range after full charge (= CastRangeGrowthMax[level], with fallback to base CastRange
        /// if growth is not configured).
        /// </summary>
        public float GetMaxChargeRange()
        {
            int level = Math.Clamp(CastInfo.SpellLevel, (byte)0, (byte)(SpellData.CastRangeGrowthMax.Length - 1));
            float growthMax = SpellData.CastRangeGrowthMax[level];
            return growthMax > 0f ? growthMax : GetCurrentCastRange();
        }

        /// <summary>
        /// Current charge range interpolated between <see cref="GetCurrentCastRange"/> (min, at 0%
        /// charge) and <see cref="GetMaxChargeRange"/> (max, at 100% charge over CastRangeGrowthDuration).
        /// </summary>
        public float GetCurrentChargeRange()
        {
            float minRange = GetCurrentCastRange();
            float maxRange = GetMaxChargeRange();
            if (maxRange <= minRange) return minRange;

            int level = Math.Clamp(CastInfo.SpellLevel, (byte)0, (byte)(SpellData.CastRangeGrowthDuration.Length - 1));
            float growthDuration = SpellData.CastRangeGrowthDuration[level];
            if (growthDuration <= 0f) return maxRange;

            float elapsed = GetChargeElapsed();
            float progress = Math.Clamp(elapsed / growthDuration, 0f, 1f);
            return minRange + (maxRange - minRange) * progress;
        }

        public string GetStringForSlot()
        {
            return CastInfo.SpellSlot switch
            {
                0 => "Q",
                1 => "W",
                2 => "E",
                3 => "R",
                62 => "Passive",
                var n when (n <= 81 && n >= 64) => "Attack",
                _ => "undefined"
            };
        }

        public void LevelUp()
        {
            if (CastInfo.SpellLevel <= 5)
            {
                ++CastInfo.SpellLevel;
            }

            if (CastInfo.SpellSlot < 4)
            {
                CastInfo.Owner.Stats.ManaCost[CastInfo.SpellSlot] = SpellData.ManaCost[CastInfo.SpellLevel];
            }
        }

        public void LowerCooldown(float lowerValue, bool silent = false)
        {
            SetCooldown(CurrentCooldown - lowerValue, silent: silent);
        }

        public void ResetSpellCast()
        {
            State = SpellState.STATE_READY;
            CurrentCastTime = 0;
            CurrentChannelDuration = 0;
            CurrentDelayTime = 0;
            // A cancelled windup (CastCancelCheck: CC/force-move clears CanCast, or lost target) must
            // also drop the owner's cast-spell pointer. CanMove()/CanChangeWaypoints() (ObjAIBase) gate
            // on _castingSpell == null, so a dangling pointer freezes the unit's movement until death —
            // Champion.Respawn is the only other SetCastSpell(null). Deactivate() already pairs these two;
            // the CastCancelCheck path forgot the second step. Guarded so we only clear OUR own cast.
            if (CastInfo.Owner.GetCastSpell() == this)
            {
                CastInfo.Owner.SetCastSpell(null);
            }
        }

        /// <summary>
        /// Adds the specified unit to the list of targets for this spell.
        /// </summary>
        /// <param name="target">Unit to remove.</param>
        public void AddTarget(AttackableUnit target)
        {
            CastInfo.AddTarget(target);

            if ((State == SpellState.STATE_CASTING || State == SpellState.STATE_CHANNELING)
                && CastInfo.Targets.Count == 0)
            {
                RefreshCurrentTarget();
            }
        }

        /// <summary>
        /// Removes the specified unit from the list of targets for this spell.
        /// </summary>
        /// <param name="target">Unit to remove.</param>
        public void RemoveTarget(AttackableUnit target)
        {
            if (!CastInfo.RemoveTarget(target))
            {
                return;
            }

            if ((State == SpellState.STATE_CASTING || State == SpellState.STATE_CHANNELING)
                && CastInfo.Targets.Count > 0
                && CastInfo.Targets[0].Unit != null)
            {
                RefreshCurrentTarget();
            }
            else
            {
                if (State == SpellState.STATE_CASTING)
                {
                    CastCancelCheck();
                }
                if (State == SpellState.STATE_CHANNELING)
                {
                    ChannelCancelCheck();
                }
            }
        }

        /// <summary>
        /// Sets the current target of this spell to the given unit.
        /// </summary>
        /// <param name="target">Unit to target.</param>
        public void SetCurrentTarget(AttackableUnit target)
        {
            if (target != null && target != CastInfo.Owner)
            {
                CastInfo.SetTarget(target, 0);
                RefreshCurrentTarget();
            }
        }

        private void RefreshCurrentTarget()
        {
            if (CastInfo.IsAutoAttack)
            {
                CastInfo.Owner.SetTargetUnit(CastInfo.Targets[0].Unit, true);

                ApiEventManager.OnPreAttack.Publish(CastInfo.Owner, this);

                if (IsBasicAttackSlotSpell() && !CastInfo.IsSecondAutoAttack)
                {
                    _game.PacketNotifier.NotifyBasic_Attack_Pos(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit);
                }
                else if (IsBasicAttackSlotSpell())
                {
                    _game.PacketNotifier.NotifyBasic_Attack(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit, CastInfo.Owner.HasMadeInitialAttack);
                }
            }

            _game.PacketNotifier.NotifyS2C_UnitSetLookAt(CastInfo.Owner, CastInfo.Targets[0].Unit, _attackType);
        }

        /// <summary>
        /// Toggles the auto cast state for this spell.
        /// </summary>
        public void SetAutocast()
        {
            _game.PacketNotifier.NotifyNPC_SetAutocast(CastInfo.Owner, this);
        }

        /// <summary>
        /// Changes a property of this spell (icon index, name, range, targeting type and etc.) on the
        /// owning player's client/HUD.
        /// </summary>
        public void ChangeSpellData(ChangeSlotSpellDataType changeType, bool isSummonerSpell = false,
            TargetingType targetingType = TargetingType.Invalid, string newName = "", float newRange = 0,
            float newMaxCastRange = 0, float newDisplayRange = 0, byte newIconIndex = 0x0,
            List<uint> offsetTargets = null)
        {
            if (CastInfo.Owner is Champion champion)
            {
                _game.PacketNotifier.NotifyChangeSlotSpellData(champion.ClientId, champion,
                    (byte)CastInfo.SpellSlot, changeType, isSummonerSpell, targetingType, newName,
                    newRange, newMaxCastRange, newDisplayRange, newIconIndex, offsetTargets);
            }
        }

        /// <summary>
        /// Overrides the normal cast range for this spell. Set to 0 to revert.
        /// </summary>
        /// <param name="newCastRange">Cast range to set.</param>
        public void SetOverrideCastRange(float newCastRange)
        {
            _overrrideCastRange = newCastRange;
            ChangeSpellData(ChangeSlotSpellDataType.Range, newRange: newCastRange);
        }

        private void SetPassiveCooldownStats(float newCd)
        {
            if (CastInfo.SpellSlot != (int)SpellSlotType.PassiveSpellSlot)
            {
                return;
            }

            var stats = CastInfo.Owner?.Stats;
            if (stats == null)
            {
                return;
            }

            if (newCd <= 0)
            {
                stats.PassiveCooldownEndTime = 0;
                stats.PassiveCooldownTotalTime = 0;
                return;
            }

            var nowSeconds = _game.GameTime / 1000.0f;
            stats.PassiveCooldownTotalTime = newCd;
            stats.PassiveCooldownEndTime = nowSeconds + newCd;
        }

        /// <summary>
        /// Sets the cooldown of this spell.
        /// </summary>
        /// <param name="newCd">Cooldown to set.</param>
        /// <param name="ignoreCDR">Whether or not to ignore cooldown reduction.</param>
        /// <param name="silent">Skip the CHAR_SetCooldown wire-broadcast. Use for passive procs that
        /// have their own dedicated trigger packet (e.g. Katarina Voracity, where slot=3
        /// isSummonerSpell=true is the canonical wire-signal and per-slot CHAR_SetCooldown for QWER
        /// is replay-empirical never sent). Server-side State/CurrentCooldown still update normally.</param>
        public void SetCooldown(float newCd, bool ignoreCDR = false, bool silent = false)
        {
            if (newCd <= 0)
            {
                if (!silent)
                {
                    _game.PacketNotifier.NotifyCHAR_SetCooldown(CastInfo.Owner, CastInfo.SpellSlot, 0, 0);
                }
                SetPassiveCooldownStats(0);
                // Changing the state of the spell to READY during casting prevents the spell from finishing casting, thus locking the player in the move order CastSpell.
                if (State != SpellState.STATE_CASTING && State != SpellState.STATE_CHANNELING)
                {
                    bool wasOnCooldown = State == SpellState.STATE_COOLDOWN;
                    State = SpellState.STATE_READY;
                    CurrentCooldown = 0;
                    // A manual reset/refund (CDR proc, item, refund) that ends an active cooldown fires the
                    // off-cooldown event too — but only when actually leaving COOLDOWN, not on a READY->READY set.
                    if (wasOnCooldown)
                    {
                        ApiEventManager.OnSpellCooldownEnd.Publish(this);
                    }
                }
            }
            else
            {
                if (!ignoreCDR)
                {
                    newCd *= 1 + CastInfo.Owner.Stats.GetClampedCooldownReduction();
                }

                if (!silent)
                {
                    _game.PacketNotifier.NotifyCHAR_SetCooldown(CastInfo.Owner, CastInfo.SpellSlot, newCd, GetCooldown());
                }
                // Mirror the guard in the newCd<=0 branch above: don't clobber STATE_CASTING/CHANNELING
                // mid-flight. Caller scenario this protects against: a passive (Katarina Voracity) calling
                // LowerCooldown on the channeling spell during the channel. Without this guard, State
                // flips to COOLDOWN, the next Update tick skips ChannelCancelCheck/StopChanneling, and the
                // channel ends without any OnSpellChannelCancel/OnSpellPostChannel event being published.
                // CurrentCooldown still updates so the post-channel cooldown reflects the reduction.
                if (State != SpellState.STATE_CASTING && State != SpellState.STATE_CHANNELING)
                {
                    State = SpellState.STATE_COOLDOWN;
                }
                CurrentCooldown = newCd;
                SetPassiveCooldownStats(newCd);
            }
        }

        public void SetLevel(byte toLevel)
        {
            if (toLevel <= 5)
            {
                CastInfo.SpellLevel = toLevel;
            }

            if (CastInfo.SpellSlot < 4)
            {
                CastInfo.Owner.Stats.ManaCost[CastInfo.SpellSlot] = SpellData.ManaCost[CastInfo.SpellLevel];
            }

            if (CastInfo.Owner is Champion champion)
            {
                _game.PacketNotifier.NotifyS2C_SetSpellLevel(_game.PlayerManager.GetClientInfoByChampion(champion).ClientId, champion.NetId, CastInfo.SpellSlot, toLevel);
            }
        }

        public void SetSpellState(SpellState state)
        {
            State = state;
        }


        public void SetSpellToggle(bool toggle)
        {
            Toggle = toggle;

            if (CastInfo.Owner is Champion ch)
            {
                var clientInfo = _game.PlayerManager.GetClientInfoByChampion(ch);
                _game.PacketNotifier.NotifyS2C_UpdateSpellToggle(clientInfo.ClientId, this);
            }
        }

        public void SetToolTipVar<T>(int tipIndex, T value) where T : struct
        {
            ToolTipData.Update(tipIndex, value);

            if (CastInfo.Owner is Champion champ)
            {
                champ.AddToolTipChange(ToolTipData);
            }
        }
        public void UpdateCharge(Vector3 position, bool forceStop)
        {
            if (State != SpellState.STATE_CHANNELING)
            {
                return;
            }

            // Server-side enforcement of SpellData.CancelChargeOnRecastTime (seconds).
            // The S4 client gates this in HudSpellLogic::IsChargeSpellRecastable (won't
            // even send NPC_CastSpellReq during the grace period), but defensive check
            // protects against modified clients. Matches Riot's design intent:
            //   VelkozR: 0.75s   SionR: 0.5s   (others: 0 default → no gate)
            // Negative value would mean "always recastable" — we treat 0 as the disable.
            if (forceStop && SpellData.CancelChargeOnRecastTime > 0f
                && GetChargeElapsed() < SpellData.CancelChargeOnRecastTime)
            {
                return;
            }

            // forceStop means the channel ends right after the script handler below (recast/fire).
            // Release the engine action-lock NOW (idempotent) so a fire handler that casts its
            // payload inside OnSpellChargeUpdate isn't blocked by the channel's own CanCast=false.
            // (Payloads cast in OnSpellChargeFire are already safe — FinishChanneling/ExpireCharge
            // release before publishing that event.)
            if (forceStop)
            {
                ReleaseChannelStatusLock();
            }

            CastInfo.TargetPosition = position;
            CastInfo.TargetPositionEnd = position;

            using (Profiler.Scope($"spell:{CastInfo?.Owner?.Model ?? "?"}/{SpellName}.OnSpellChargeUpdate", "scripts"))
            {
                Script.OnSpellChargeUpdate(this, position, forceStop);
            }

            if (forceStop)
            {
                // For channel-lock charge spells (PreventChargingSecondCast=1, e.g. Vel'Koz R, Sion R)
                // a player-recast mid-channel needs NPC_InstantStop_Attack to clear the client's HUD
                // charge-bar and break the channel animation lock. Replay-verified on Vel'Koz R
                // (a6db3774 cast @1000030 → ISA @+1782ms during channel; natural completion @other
                // casts → no ISA). The ISA fires BEFORE StopChanneling so it lands while State is
                // still STATE_CHANNELING (consistent with the StopChanneling Cancel path semantics).
                // Sion R's slam still triggers via OnSpellChargeFire after the routing below — the
                // ISA just additionally clears HUD/anim lock that Fire-path alone doesn't address.
                //
                // For tap-charge (Varus Q): Riot replay confirms ZERO ISA on release-fire
                // (ecb101fa + ea71bf6f, 129 events) — release IS the fire trigger and the channel
                // ends naturally on the client. Don't broadcast ISA here.
                if (IsChannelLockCharge)
                {
                    _game.PacketNotifier.NotifyNPC_InstantStop_Attack(CastInfo.Owner, false);
                }

                // For charge-channel spells (UseChargeChanneling=1, e.g. Varus Q) the client-driven
                // button-release IS the fire trigger, semantically equivalent to the server's natural
                // timeout: route through (Success, TimeCompleted) so FinishChanneling() runs,
                // OnSpellPostChannel fires the missile, State→STATE_COOLDOWN.
                // Non-charge channels (Katarina R, Recall etc.) keep the original PlayerCommand-cancel
                // path because for them "release" actually means abort.
                if (IsChargeSpell)
                {
                    StopChanneling(ChannelingStopCondition.Success, ChannelingStopSource.TimeCompleted);
                }
                else
                {
                    StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.NotCancelled);
                }
            }
        }
        public SpellMissile CreateCustomMissile(Vector2 start, Vector2 end, MissileParameters parameters, bool isForceCastingOrChannel = false, bool isOverrideCastPosition = true, float? customHeightOffset = null, AttackableUnit target = null)
        {
            var netId = _networkIdManager.GetNewNetId();
            var castInfoClone = CastInfo.Clone();

            float heightOffset = customHeightOffset ?? SpellData.MissileTargetHeightAugment;

            float targetHeight = CastInfo.Owner.GetHeight() + heightOffset;

            castInfoClone.TargetPosition = new Vector3(end.X, targetHeight, end.Y);
            castInfoClone.TargetPositionEnd = new Vector3(end.X, targetHeight, end.Y);

            if (start == CastInfo.Owner.Position)
            {
                var pos3D = CastInfo.Owner.GetPosition3D();
                castInfoClone.SpellCastLaunchPosition = new Vector3(pos3D.X, pos3D.Y + heightOffset, pos3D.Z);
            }
            else
            {
                float startGroundHeight = _game.Map.NavigationGrid.GetHeightAtLocation(start);
                castInfoClone.SpellCastLaunchPosition = new Vector3(start.X, startGroundHeight + heightOffset, start.Y);
            }

            if (castInfoClone.Targets == null) castInfoClone.Targets = new List<CastTarget>();
            if (castInfoClone.Targets.Count == 0)
            {
                castInfoClone.Targets.Add(new CastTarget(target, HitResult.HIT_Normal));
            }
            else
            {
                castInfoClone.Targets[0] = new CastTarget(target, HitResult.HIT_Normal);
            }

            castInfoClone.IsForceCastingOrChannel = isForceCastingOrChannel;
            castInfoClone.IsOverrideCastPosition = isOverrideCastPosition;

            castInfoClone.MissileNetID = netId;

            // Replay-verified (Talon W return blades: 162 MissileReplications across two
            // replays/perspectives; also Varus Q): Riot DOES replicate script-spawned
            // sub-missiles via MissileReplication — a non-empty MissileEffect does NOT mean
            // server-only; the client renders the missile effect FROM the replicated missile.
            // The old `isServerOnly = MissileEffect != ""` heuristic made every such missile
            // invisible (see project_deferred_sub_missile_replication).
            const bool isServerOnly = false;

            SpellMissile p = null;

            // Script-side CollisionRadius override falls back to SpellData.LineWidth when null.
            // Useful for missiles whose visible hit area doesn't match the JSON LineWidth
            // (e.g. Diana W orbs have LineWidth=0 in JSON but a 175u visual hit radius).
            int collisionRadius = parameters.CollisionRadius ?? (int)SpellData.LineWidth;

            switch (parameters.Type)
            {
                case MissileType.Target:
                    p = new SpellMissile(_game, collisionRadius, this, castInfoClone, SpellData.MissileSpeed, netId, isServerOnly);
                    break;
                case MissileType.Chained:
                    p = new SpellChainMissile(_game, collisionRadius, this, castInfoClone, parameters, SpellData.MissileSpeed, netId, isServerOnly);
                    break;
                case MissileType.Arc:
                    p = new SpellLineMissile(_game, collisionRadius, this, castInfoClone, SpellData.MissileSpeed, end, netId, isServerOnly);
                    break;
                case MissileType.Circle:
                    p = new SpellCircleMissile(_game, collisionRadius, this, castInfoClone, SpellData.MissileSpeed, end, netId, isServerOnly);
                    break;
            }

            if (p == null || (p.HasDestination() && p.Position == p.Destination) || p.Position == p.GetTargetPosition())
            {
                return null;
            }

            // Script-spawned missiles have no pending client cast (no CastSpellAns was sent
            // for them), so they must take the MissileReplication spawn path. MUST be set
            // before AddObject — the spawn packet is constructed inline there.
            p.HasClientCastInfo = false;

            _game.ObjectManager.AddObject(p);
            ApiEventManager.OnLaunchMissile.Publish(this, p);

            return p;
        }

        /// <summary>
        /// Like <see cref="CreateCustomMissile"/> but explicitly client-visible — sets
        /// <c>HasClientCastInfo=false</c> so the spawn broadcast emits <c>MissileReplication</c>
        /// instead of being suppressed by <c>ConstructSpawnPacket</c>'s primary-missile shortcut.
        /// Use for sub-missiles that need their own visual replication: e.g. Varus Q's
        /// VarusQMissile, where the parent VarusQ cast has <c>MissileEffect=""</c> in JSON so
        /// the client can't render the missile from the parent CastSpellAns alone.
        /// </summary>
        public SpellMissile CreateReplicatedMissile(Vector2 start, Vector2 end, MissileParameters parameters,
            bool isForceCastingOrChannel = false, bool isOverrideCastPosition = true,
            float? customHeightOffset = null, AttackableUnit target = null)
        {
            var p = CreateCustomMissile(start, end, parameters, isForceCastingOrChannel,
                isOverrideCastPosition, customHeightOffset, target);
            if (p != null)
            {
                // Explicit MissileReplication broadcast. (Setting p.HasClientCastInfo=false would
                // be dead code — the auto-spawn-packet check in ConstructSpawnPacket already ran
                // inside CreateCustomMissile→AddObject and decided no broadcast based on the
                // default true. We bypass that with a manual NotifyMissileReplication.)
                _game.PacketNotifier.NotifyMissileReplication(p);
            }
            return p;
        }

        /// <summary>
        /// Re-broadcasts <c>NPC_CastSpellAns</c> for this spell with updated target positions,
        /// reusing the original <c>SpellNetID</c> + <c>MissileNetID</c> from the initial cast.
        /// Designed for charge-channel fire (e.g. Varus Q release): replay-verified pattern
        /// (ecb101fa Varus-POV, 129 events) emits a second CastSpellAns at fire with same
        /// NetIDs and updated <c>targetPos</c>/<c>launchPos</c>. This packet primes the client
        /// to spawn the missile visual via the AlternateName chain (VarusQ → VarusQMissile.json)
        /// and clears the charge bar.
        /// </summary>
        public void NotifyChargeFireCastSpellAns(Vector3 newTargetPos)
        {
            CastInfo.TargetPosition = newTargetPos;
            CastInfo.TargetPositionEnd = newTargetPos;
            CastInfo.SpellCastLaunchPosition = CastInfo.Owner.GetPosition3D();
            // isContinuationFromExistingCast=true → bitfield bit 0 set in the wire packet.
            // S4 client (obj_AI_Base_PImpl_Int.cpp:2987-3050) uses this flag to route through
            // SpellbookRouter::ChooseSpellbook + match against currently-casting SpellInstanceClient
            // by SpellNetID. Without it, the client treats the fire-CastSpellAns as a fresh cast and
            // fails to connect it to the existing charge-state SpellInstanceClient → no missile spawn.
            _game.PacketNotifier.NotifyNPC_CastSpellAns(this, isContinuationFromExistingCast: true);
        }

        /// <summary>
        /// Script-friendly wrapper around <see cref="NotifyChargeFireCastSpellAns"/>. Call from a
        /// charge spell's recast-fire handler (e.g. <see cref="ISpellScript.OnSpellChargeFire"/> or
        /// equivalent custom fire path) to broadcast the parent channel-end CastSpellAns that
        /// clears the client's charge HUD and signals "charge completed and triggered effects".
        /// Takes the fire-time impact target in 2D world coords; height is filled from the caster.
        /// <para>Only emit when the recast actually triggers effects (Sion Q/R, Varus Q, Xerath Q).
        /// Do NOT call for cancel-only recasts (Vel'Koz R) — those rely on the channel-cancel
        /// pipeline's <c>NPC_InstantStop_Attack</c> / silent-timeout behavior instead.</para>
        /// </summary>
        public void FireCharge(Vector2 targetPos)
        {
            NotifyChargeFireCastSpellAns(new Vector3(targetPos.X, CastInfo.Owner.GetHeight(), targetPos.Y));
        }

        /// <summary>
        /// Called every diff milliseconds to update the spell
        /// </summary>
        public void Update(float diff)
        {
            if (!HasEmptyScript)
            {
                // PersistsThroughDeath: a spell's per-tick script logic (OnUpdate) stops once its
                // caster dies, UNLESS the spell is flagged to persist (DoT zones, traps, thrown
                // projectiles — Riot has ~hundreds). Source mirrors Riot's accessor: either the
                // SpellData flag (kSpellFlagPersistThroughDeath = 0x8) or the script-level property
                // (e.g. Tormented Soil sets only the latter — its JSON flag bit is unset).
                bool ownerDead = CastInfo?.Owner is { IsDead: true };
                bool persists = Script.ScriptMetadata.PersistsThroughDeath
                                || SpellData.Flags.HasFlag(SpellDataFlags.PersistThroughDeath);
                if (!ownerDead || persists)
                {
                    try
                    {
                        using var _scope = Profiler.Scope($"spell:{CastInfo?.Owner?.Model ?? "?"}/{SpellName}.OnUpdate", "scripts");
                        Script.OnUpdate(diff);
                    }
                    catch(Exception e)
                    {
                        _logger.Error(null, e);
                    }
                }
            }

            switch (State)
            {
                case SpellState.STATE_READY:
                    {
                        break;
                    }
                case SpellState.STATE_CASTING:
                    {
                        if (CastCancelCheck())
                        {
                            break;
                        }
                        if (!CastInfo.IsAutoAttack && !CastInfo.UseAttackCastTime)
                        {
                            CurrentCastTime -= diff / 1000.0f;
                            if (CurrentCastTime <= 0)
                            {
                                FinishCasting();
                                if (GetEffectiveChannelDuration() > 0)
                                {
                                    Channel();
                                }
                            }
                        }
                        else
                        {
                            CurrentDelayTime += diff / 1000.0f;
                            // DesignerCastTime is already attack-speed-scaled at cast time
                            // (AA block divides by AttackSpeedModifier so the CastSpellAns
                            // carries the wire-true scaled windup) — dividing again here
                            // would finish the windup modifier² too fast.
                            if (CurrentDelayTime >= CastInfo.DesignerCastTime)
                            {
                                FinishCasting();
                            }
                        }
                        break;
                    }
                case SpellState.STATE_COOLDOWN:
                    {
                        CurrentCooldown -= diff / 1000.0f;
                        if (CurrentCooldown < 0)
                        {
                            State = SpellState.STATE_READY;
                        }
                        break;
                    }
                case SpellState.STATE_CHANNELING:
                    {
                        CurrentChannelDuration -= diff / 1000.0f;
                        ChannelCancelCheck();
                        if (State == SpellState.STATE_CHANNELING)
                        {
                            if (IsChargeSpell)
                            {
                                // OnSpellChargeTick fires every server tick (like a normal Update);
                                // scripts pace their own logic off `diff` (e.g. a damage PeriodicTicker
                                // for Vel'Koz R, a charge-time accumulator for Sion Q/R). It is NOT gated
                                // by ChargeUpdateInterval — that field is the CLIENT's charge-update SEND
                                // cadence and already paces OnSpellChargeUpdate (where steering lives).
                                // Pacing the server tick to it too was only needed when steering ran in
                                // the tick; now steering is in OnSpellChargeUpdate, so the tick ticks free.
                                ApiEventManager.OnSpellChargeTick.Publish(this, diff);
                            }
                            else
                            {
                                ApiEventManager.OnSpellChannelUpdate.Publish(this, diff);
                            }
                        }
                        if (State == SpellState.STATE_CHANNELING && CurrentChannelDuration <= 0)
                        {
                            if (IsChargeSpell)
                            {
                                // Charge expired without manual release — refund-or-fire policy
                                // is in the script's OnSpellChargeCancel(TimeCompleted) handler.
                                // Engine handles cooldown + state cleanup like a normal completion.
                                ExpireCharge();
                            }
                            else
                            {
                                // Normal channel completed naturally — proceed to FinishChanneling
                                // → OnSpellPostChannel.
                                StopChanneling(ChannelingStopCondition.Success, ChannelingStopSource.TimeCompleted);
                            }
                        }
                        break;
                    }
            }
            if (CurrentAmmo < SpellData.MaxAmmo && CastInfo.SpellLevel > 0)
            {
                CurrentAmmoCooldown -= diff / 1000.0f;

                if (CurrentAmmoCooldown <= 0)
                {
                    AddAmmo(Script.ScriptMetadata.AmmoPerCharge);
                }
            }
        }
    }
}
