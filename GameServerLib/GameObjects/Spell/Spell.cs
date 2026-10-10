using GameServerCore.Content;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
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
using GameServerLib.GameObjects;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS
{
    public class SpellCastInfo
    {
        public bool CouldCast { get; set; }
        public bool WasQueued { get; set; }
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
        private float? _manaCostOverride;
        private AttackType _attackType;
        private bool _cancelledInPreCast;
        private bool _preCastInProgress;
        private bool _hasActiveCooldownOverride;
        private uint _castVersion;
        private float _castTimeBeforeStart;
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
        public float CooldownTotalTime { get; private set; }
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
        /// Zero based index of this spell's inventory icon in the client data.
        /// </summary>
        public byte IconIndex { get; private set; }
        /// <summary>
        /// Spell data for this spell used for interactions between units, cooldown, channeling time, etc. Refer to SpellData class.
        /// </summary>
        public SpellData SpellData { get; }

        public float GetManaCost(int? level = null)
            => _manaCostOverride ?? SpellData.ManaCost[Math.Clamp(level ?? CastInfo.SpellLevel, 0, SpellData.ManaCost.Length - 1)];
        public bool HasManaCostOverride => _manaCostOverride.HasValue;

        /// <summary>Override this spell's cost without changing cached content shared by other actors.</summary>
        public void SetManaCostOverride(float? cost)
        {
            if (cost.HasValue && (!float.IsFinite(cost.Value) || cost.Value < 0))
                throw new ArgumentOutOfRangeException(nameof(cost));
            if (_manaCostOverride == cost) return;
            _manaCostOverride = cost;
            if (CastInfo.SpellSlot < 4)
                CastInfo.Owner.Stats.ManaCost[CastInfo.SpellSlot] = GetManaCost();
            if (CastInfo.Owner is Champion champion
                && _game.PlayerManager.GetClientInfoByChampion(champion) is { } client)
                _game.PacketNotifier.NotifySpellManaCostOverride(client.ClientId, this);
        }
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

        public Spell(Game game, ObjAIBase owner, string spellName, byte slot, bool enableScripts = true)
        {
            _game = game;
            _networkIdManager = game.NetworkIdManager;
            CastInfo.MissileNetID = _networkIdManager.GetNewNetId();
            CastInfo.ClientCastRecipients.Clear();
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
                    HasEmptyScript = Script is SpellScriptEmpty;
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
            if (Script != null) ApiEventManager.RemoveAllListenersForOwner(Script);
            _scriptActivated = false;
            _scriptPostActivated = false;

            string nameSpace = "Spells";
            if (CastInfo.SpellSlot >= (byte)SpellSlotType.InventorySlots && CastInfo.SpellSlot < (byte)SpellSlotType.BluePillSlot)
            {
                nameSpace = "ItemSpells";
            }

            Script = _game.ScriptEngine.CreateObject<ISpellScript>(nameSpace, SpellName);

            if (Script == null)
            {
                string fallbackScript = "";

                if (SpellName.Contains("Turret") && SpellName.EndsWith("BasicAttack"))
                {
                    fallbackScript = "TurretBasicAttack";
                }
                else if (CastInfo.Owner is Monster &&
                         (SpellName.EndsWith("BasicAttack") ||
                          SpellName.EndsWith("BasicAttack2") ||
                          SpellName.EndsWith("BasicAttack3") ||
                          SpellName.EndsWith("BasicAttack4") ||
                          SpellName.Equals("WormAttack") ||
                          SpellName.Equals("DragonBasicAttack")))
                {
                    fallbackScript = "MonsterBasicAttack";
                }

                if (!string.IsNullOrEmpty(fallbackScript))
                {
                    Script = _game.ScriptEngine.CreateObject<ISpellScript>(nameSpace, fallbackScript) ?? new SpellScriptEmpty();
                }
                else
                {
                    Script = new SpellScriptEmpty();
                }
            }

            if (Script.ScriptMetadata.TriggersSpellCasts)
            {
                ApiEventManager.OnSpellCast.AddListener(Script, this, Script.OnSpellCast);
                ApiEventManager.OnSpellPostCast.AddListener(Script, this, Script.OnSpellPostCast);
            }

            if (Script.ScriptMetadata.ChannelDuration > 0)
            {
                ApiEventManager.OnSpellChannel.AddListener(Script, this, Script.OnSpellChannel);
                ApiEventManager.OnSpellChannelCancel.AddListener(Script, this, Script.OnSpellChannelCancel);
                ApiEventManager.OnSpellPostChannel.AddListener(Script, this, Script.OnSpellPostChannel);
            }

            //Activate spell - Notes: Deactivate is never called as spell removal hasn't been added
            try
            {
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
                Script.OnPostActivate(CastInfo.Owner, this);
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }
        }

        public void ApplyEffects(AttackableUnit u, SpellMissile m = null, SpellSector s = null)
        {
            if (!u.GetIsTargetableToTeam(CastInfo.Owner.Team))
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

            if (!CastInfo.IsAutoAttack)
            {
                ApiEventManager.OnSpellHit.Publish(this, (u, m, s));
                if (m != null)
                {
                    ApiEventManager.OnSpellMissileHit.Publish(m, u);
                }
                if (s != null)
                {
                    ApiEventManager.OnSpellSectorHit.Publish(s, u);
                }

                ApiEventManager.OnBeingSpellHit.Publish(u, (this, m, s));
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

        private void SetAttackWindupTiming()
        {
            var attacks = CastInfo.Owner.CharData.BasicAttacks;
            int index = CastInfo.SpellSlot - (int)SpellSlotType.BasicAttackNormalSlots;
            if (index < 0 || index >= attacks.Count) index = 0;
            var attack = attacks[index];
            float modifier = Math.Max(0.0001f, CastInfo.AttackSpeedModifier);
            CastInfo.AttackSpeedModifier = modifier;
            float duration = SpellData.GetCharacterAttackCastDelay(modifier,
                attack.AttackDelayOffsetPercent, attack.AttackDelayCastOffsetPercent,
                attack.AttackDelayCastOffsetPercentAttackSpeedRatio);

            CastInfo.DesignerCastTime = duration * modifier;
            if (CastInfo.IsAutoAttack && IsBasicAttackSlotSpell())
                CastInfo.DesignerTotalTime = SpellData.GetCharacterAttackDelay(modifier,
                    attack.AttackDelayOffsetPercent) * modifier;
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
                CancelCast();
                return true;
            }

            if (!CastInfo.IsAutoAttack && SpellData.CantCancelWhileWindingUp)
            {
                return false;
            }

            if (CastInfo.Targets.Count > 0 && CastInfo.Targets[0].Unit != null)
            {
                // Uncancellable.
                if (SpellData.CantCancelWhileWindingUp)
                {
                    return false;
                }

                var spellTarget = CastInfo.Targets[0].Unit;

                bool canAffectUntargetable = !CastInfo.IsAutoAttack && SpellData.Flags.HasFlag(SpellDataFlags.AffectUntargetable);
                bool canAffectDead = !CastInfo.IsAutoAttack && SpellData.Flags.HasFlag(SpellDataFlags.AffectDead);
                if (spellTarget != CastInfo.Owner
                    && (!spellTarget.IsVisibleByTeam(CastInfo.Owner.Team)
                    || (!canAffectUntargetable && !spellTarget.Status.HasFlag(StatusFlags.Targetable) && !spellTarget.CharData.IsUseable)
                    || (!canAffectUntargetable && !spellTarget.GetIsTargetableToTeam(CastInfo.Owner.Team) && !spellTarget.CharData.IsUseable)
                    || (!canAffectDead && spellTarget.IsDead)))
                {
                    CancelCast();
                    return true;
                }

                // Regular auto attacks can lose their target due to untargetability and distance.
                float cancelBuffer = 300.0f;
                float maxCancelRange = CastInfo.Owner.Stats.Range.Total + spellTarget.CollisionRadius + CastInfo.Owner.CollisionRadius + cancelBuffer;
                if (CastInfo.IsAutoAttack
                && (spellTarget != CastInfo.Owner.TargetUnit
                || Vector2.Distance(spellTarget.Position, CastInfo.Owner.Position) > maxCancelRange // TODO: Verify if edge-to-edge
                || CastInfo.Owner.GetCastSpell() != null
                || CastInfo.Owner.ChannelSpell != null))
                {
                    CancelCast();
                    return true;
                }
            }
            else
            {
                if (CastInfo.IsAutoAttack)
                {
                    CancelCast();
                    return true;
                }
            }


            var status = CastInfo.Owner.Status;

            if ((!CastInfo.IsAutoAttack && !CastInfo.Owner.CanCastByStatus(this))
             || (CastInfo.IsAutoAttack && (status.HasFlag(StatusFlags.Charmed)
             || status.HasFlag(StatusFlags.Feared)
             || status.HasFlag(StatusFlags.Stunned)
             || status.HasFlag(StatusFlags.Suppressed)
             || status.HasFlag(StatusFlags.Taunted)
             || status.HasFlag(StatusFlags.Sleep)
             || status.HasFlag(StatusFlags.Pacified)
             || status.HasFlag(StatusFlags.Disarmed) || !status.HasFlag(StatusFlags.CanAttack))))
            {
                CancelCast();
                return true;
            }

            return false;
        }

        public bool Cast(Vector2 start, Vector2 end, AttackableUnit unit = null)
        {
            if (_preCastInProgress || !float.IsFinite(start.X) || !float.IsFinite(start.Y)
                || !float.IsFinite(end.X) || !float.IsFinite(end.Y)
                || (unit == null && SpellData.TargetingType == TargetingType.Target)
                || (ShouldTreatAsAutoAttack() && CastInfo.Owner.MovementParameters != null)
                || (!ShouldTreatAsAutoAttack() && !CastInfo.Owner.CanCastByStatus(this)))
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

            if (!ShouldTreatAsAutoAttack() && unit != null && unit != CastInfo.Owner
                && !SpellData.IsValidTarget(CastInfo.Owner, unit))
            {
                return false;
            }

            _attackType = AttackType.ATTACK_TYPE_RADIAL;
            var stats = CastInfo.Owner.Stats;

            // Auto-attack overrides can use non-AA slots (ex: TalonNoxianDiplomacyAttack).
            // If this spell is currently selected as the owner's attack spell, keep AA behavior.
            CastInfo.IsAutoAttack = ShouldTreatAsAutoAttack();

            if ((_game.Config.GameFeatures.HasFlag(FeatureFlags.EnableManaCosts)
                && GetManaCost() * (1 - stats.SpellCostReduction) > stats.CurrentMana && !CastInfo.IsAutoAttack)
                || State != SpellState.STATE_READY || CurrentAmmo <= 0)
            {
                return false;
            }

            CastInfo.SpellNetID = _networkIdManager.GetNewNetId();

            CastInfo.AttackSpeedModifier = stats.AttackSpeedMultiplier.Total;

            CastInfo.MissileNetID = _networkIdManager.GetNewNetId();
            CastInfo.ClientCastRecipients.Clear();

            CastInfo.TargetPosition = new Vector3(start.X, _game.Map.NavigationGrid.GetHeightAtLocation(start), start.Y);
            CastInfo.TargetPositionEnd = new Vector3(end.X, _game.Map.NavigationGrid.GetHeightAtLocation(end), end.Y);

            CastInfo.Targets.Clear();

            // TODO: Unhardcode (wind down? If so, make it cancelable via casting a different spell and via changing move order to AttackTo or MoveTo.)
            CastInfo.ExtraCastTime = 0.0f;
            CastInfo.Cooldown = GetCooldown();
            // TODO: Unhardcode (extra windup?)
            CastInfo.StartCastTime = 0.0f;

            var trueChannel = SpellData.ChannelDuration[CastInfo.SpellLevel];
            if (Script.ScriptMetadata.ChannelDuration > 0)
            {
                trueChannel = Script.ScriptMetadata.ChannelDuration;
            }

            // For things like Garen Q, Leona Q, and TF W (and probably more)
            if (!CastInfo.IsAutoAttack && (SpellData.ConsideredAsAutoAttack || SpellData.UseAutoattackCastTime || CastInfo.UseAttackCastDelay)) // TODO: Verify
            {
                CastInfo.IsAutoAttack = false;
                CastInfo.DesignerCastTime = SpellData.GetCharacterAttackCastDelay(CastInfo.AttackSpeedModifier, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayCastOffsetPercent, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayCastOffsetPercentAttackSpeedRatio);
                CastInfo.DesignerTotalTime = SpellData.GetCharacterAttackDelay(CastInfo.AttackSpeedModifier, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent);
                CastInfo.UseAttackCastDelay = true;
            }
            else
            {
                CastInfo.DesignerCastTime = SpellData.GetCastTime();
                CastInfo.DesignerTotalTime = CastInfo.DesignerCastTime + trueChannel;
            }

            if (SpellData.OverrideCastTime > 0)
            {
                CastInfo.DesignerCastTime = SpellData.OverrideCastTime;
                CastInfo.DesignerTotalTime = SpellData.OverrideCastTime + trueChannel;
            }

            if (Script.ScriptMetadata.CastTime > 0)
            {
                CastInfo.DesignerCastTime = Script.ScriptMetadata.CastTime;
                CastInfo.DesignerTotalTime = Script.ScriptMetadata.CastTime + trueChannel;
            }

            // Otherwise, use the normal auto attack setup
            if (CastInfo.IsAutoAttack)
            {
                _attackType = AttackType.ATTACK_TYPE_TARGETED;
                CastInfo.UseAttackCastTime = true;
                CastInfo.AmmoUsed = 0; // TODO: Verify
                CastInfo.AmmoRechargeTime = 0; // TODO: Verify
                CastInfo.IsSecondAutoAttack = CastInfo.Owner.HasMadeInitialAttack;
            }
            else
            {
                CastInfo.AmmoUsed = 1; // TODO: Verify
                CastInfo.AmmoRechargeTime = CurrentAmmoCooldown; // TODO: Verify
            }

            // TODO: Account for multiple targets
            CastInfo.Targets.Add(new CastTarget(unit, CastTarget.GetHitResult(unit, CastInfo.IsAutoAttack, CastInfo.Owner.IsNextAutoCrit)));

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

            _hasActiveCooldownOverride = false;
            _cancelledInPreCast = false;
            _preCastInProgress = true;
            try
            {
                SpellData.CastOverrides.Apply(CastInfo);
                Script.OnSpellPreCast(CastInfo.Owner, this, unit, start, end);
            }
            catch(Exception e)
            {
                _logger.Error(null, e);
            }
            finally
            {
                _preCastInProgress = false;
            }
            if (_cancelledInPreCast) return false;

            if (!CastInfo.IsAutoAttack)
            {
                if (!SpellData.DoesntBreakChannels)
                {
                    CastInfo.Owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.Casting);
                }

                if (!SpellData.Flags.HasFlag(SpellDataFlags.InstantCast)
                    && CastInfo.Owner.IsAttacking && !CastInfo.Owner.HasAutoAttacked && CastInfo.Owner.AutoAttackSpell != null && !CastInfo.Owner.AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                {
                    if (CastInfo.ClientStopAttackWindupFlags.HasValue)
                    {
                        CastInfo.ClientStopBeforeCastFlags = CastInfo.ClientStopAttackWindupFlags.Value;
                        CastInfo.ClientStopBeforeCastMissileNetID = CastInfo.Owner.AutoAttackSpell.CastInfo.MissileNetID;
                    }
                    CastInfo.Owner.CancelAutoAttack(true, true, notifyClient: !CastInfo.ClientStopBeforeCastFlags.HasValue);
                }

                if (!SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                {
                    CastInfo.Owner.SetCastSpell(this);
                }
            }
            else if (unit != null)
            {
                CastInfo.Owner.SetTargetUnit(unit, true);
            }
            if (CastInfo.Owner.Status.HasFlag(StatusFlags.Stealthed) && Script.ScriptMetadata.CastingBreaksStealth)
            {
                var packetInfo = new DelayedSpellPacketInfo(this, _game.GameTime);
                CastInfo.Owner.delayedSpellPackets.Add(packetInfo);
                CastInfo.Owner.RemoveBuffsByType(BuffType.INVISIBILITY);
            }
            if (!CastInfo.IsAutoAttack && _game.Config.GameFeatures.HasFlag(FeatureFlags.EnableManaCosts))
            {
                stats.CurrentMana -= GetManaCost() * (1 - stats.SpellCostReduction);
            }

            if (!CastInfo.IsAutoAttack && !SpellData.IsToggleSpell
                        || (!SpellData.NoWinddownIfCancelled
                        && !SpellData.Flags.HasFlag(SpellDataFlags.InstantCast)
                        && SpellData.CantCancelWhileWindingUp))
            {
                if (!SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                {
                    if (!SpellData.CanMoveWhileChanneling && CastInfo.Owner.MovementParameters == null)
                    {
                        //CastInfo.Owner.StopMovement(networked:false);
                        // TODO: Verify if we should move this outside of this TriggersSpellCasts if statement.
                        CastInfo.Owner.UpdateMoveOrder(OrderType.CastSpell, true);
                    }

                    if (Script.ScriptMetadata.AutoFaceDirection)
                    {
                        var goingTo = end - CastInfo.Owner.Position;

                        if (unit != null)
                        {
                            goingTo = unit.Position - CastInfo.Owner.Position;
                        }

                        if (goingTo.LengthSquared() > 0)
                        {
                            var dirTemp = Vector2.Normalize(goingTo);
                            CastInfo.Owner.FaceDirection(new Vector3(dirTemp.X, 0, dirTemp.Y), false);
                        }
                    }
                }
            }

            // If we are supposed to automatically cast a skillshot for this spell, then calculate the proper end position before casting.
            if (Script.ScriptMetadata.MissileParameters != null && Script.ScriptMetadata.MissileParameters.Type == MissileType.Circle)
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
                if (Script.ScriptMetadata.AutoFaceDirection)
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

                SetAttackWindupTiming();

                if (CastInfo.IsAutoAttack && IsBasicAttackSlotSpell())
                {
                    if (!CastInfo.IsSecondAutoAttack)
                    {
                        _game.PacketNotifier.NotifyBasic_Attack_Pos(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit);
                    }
                    else
                    {
                        _game.PacketNotifier.NotifyBasic_Attack(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit, CastInfo.Owner.HasMadeInitialAttack);
                    }
                }

                if (CastInfo.UseAttackCastTime && !(CastInfo.IsAutoAttack && IsBasicAttackSlotSpell()))
                {
                    // TODO: Verify
                    CastInfo.DesignerTotalTime = CastInfo.DesignerCastTime + trueChannel;
                }
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
                }
                Vector3 originalTarget = CastInfo.TargetPosition;
                Vector3 originalTargetEnd = CastInfo.TargetPositionEnd;

                if (!Script.ScriptMetadata.AutoFaceDirection)
                {
                    var owner = CastInfo.Owner;
                    var forwardVec = GetSafeFacingDirection();

                    var fakePos = owner.Position + (forwardVec * 10.0f);
                    var fakePos3D = new Vector3(fakePos.X, _game.Map.NavigationGrid.GetHeightAtLocation(fakePos), fakePos.Y);

                    CastInfo.TargetPosition = fakePos3D;
                    CastInfo.TargetPositionEnd = fakePos3D;
                }
                try
                {
                    _game.PacketNotifier.NotifyNPC_CastSpellAns(this);
                }
                finally
                {
                    CastInfo.TargetPosition = originalTarget;
                    CastInfo.TargetPositionEnd = originalTargetEnd;
                }
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

            return BeginCasting();
        }
        public bool Cast(CastInfo castInfo, bool cast)
        {
            bool preserveContext = !cast && (_preCastInProgress
                || State == SpellState.STATE_CASTING || State == SpellState.STATE_CHANNELING
                || CastInfo.Owner.SpellToCast == this);
            var previousCastInfo = CastInfo;
            bool previousCancelled = _cancelledInPreCast;
            bool previousPreCast = _preCastInProgress;
            try
            {
                return CastInternal(castInfo, cast);
            }
            finally
            {
                if (preserveContext)
                {
                    CastInfo = previousCastInfo;
                    _cancelledInPreCast = previousCancelled;
                    _preCastInProgress = previousPreCast;
                }
            }
        }

        private bool CastInternal(CastInfo castInfo, bool cast)
        {
            if (castInfo?.Owner == null || (cast && (_preCastInProgress || State != SpellState.STATE_READY || CurrentAmmo <= 0)))
            {
                return false;
            }

            var stats = castInfo.Owner.Stats;
            bool isAutoAttack = castInfo.IsAutoAttack || castInfo.Owner.AutoAttackSpell == this;
            if (cast && !isAutoAttack && _game.Config.GameFeatures.HasFlag(FeatureFlags.EnableManaCosts)
                && GetManaCost(castInfo.SpellLevel) * (1 - stats.SpellCostReduction) > stats.CurrentMana)
            {
                return false;
            }

            CastInfo = castInfo;
            CastInfo.IsAutoAttack = ShouldTreatAsAutoAttack();
            if (CastInfo.Targets == null)
            {
                CastInfo.Targets = new List<CastTarget>();
            }
            if (CastInfo.Targets.Count == 0)
            {
                CastInfo.Targets.Add(new CastTarget(null, HitResult.HIT_Normal));
            }
            var start = new Vector2(CastInfo.TargetPosition.X, CastInfo.TargetPosition.Z);
            var end = new Vector2(CastInfo.TargetPositionEnd.X, CastInfo.TargetPositionEnd.Z);

            CastInfo.MissileNetID = _networkIdManager.GetNewNetId();
            CastInfo.ClientCastRecipients.Clear();

            CastInfo.ExtraCastTime = 0.0f; // TODO: Unhardcode
            CastInfo.Cooldown = GetCooldown();
            CastInfo.StartCastTime = 0.0f; // TODO: Unhardcode

            var trueChannel = SpellData.ChannelDuration[CastInfo.SpellLevel];
            if (Script.ScriptMetadata.ChannelDuration > 0)
            {
                trueChannel = Script.ScriptMetadata.ChannelDuration;
            }

            // For things like Garen Q, Leona Q, and TF W (and probably more)
            if (!CastInfo.IsAutoAttack && (SpellData.ConsideredAsAutoAttack || SpellData.UseAutoattackCastTime || CastInfo.UseAttackCastDelay)) // TODO: Verify
            {
                CastInfo.IsAutoAttack = false;
                CastInfo.DesignerCastTime = SpellData.GetCharacterAttackCastDelay(CastInfo.AttackSpeedModifier, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayCastOffsetPercent, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayCastOffsetPercentAttackSpeedRatio);
                CastInfo.DesignerTotalTime = SpellData.GetCharacterAttackDelay(CastInfo.AttackSpeedModifier, CastInfo.Owner.CharData.BasicAttacks[0].AttackDelayOffsetPercent);
                CastInfo.UseAttackCastDelay = true;
            }
            else
            {
                CastInfo.DesignerCastTime = SpellData.GetCastTime();
                CastInfo.DesignerTotalTime = CastInfo.DesignerCastTime + trueChannel;
            }

            if (SpellData.OverrideCastTime > 0)
            {
                CastInfo.DesignerCastTime = SpellData.OverrideCastTime;
                CastInfo.DesignerTotalTime = SpellData.OverrideCastTime + trueChannel;
            }

            if (Script.ScriptMetadata.CastTime > 0)
            {
                CastInfo.DesignerCastTime = Script.ScriptMetadata.CastTime;
                CastInfo.DesignerTotalTime = Script.ScriptMetadata.CastTime + trueChannel;
            }

            // Otherwise, use the normal auto attack setup
            if (CastInfo.IsAutoAttack)
            {
                CastInfo.UseAttackCastTime = true;
                CastInfo.AmmoUsed = 0; // TODO: Verify
                CastInfo.AmmoRechargeTime = 0; // TODO: Verify
                CastInfo.IsSecondAutoAttack = CastInfo.Owner.HasMadeInitialAttack;
            }
            else
            {
                CastInfo.AmmoUsed = 1; // TODO: Verify
                CastInfo.AmmoRechargeTime = CurrentCooldown; // TODO: Verify
            }

            if (cast) _hasActiveCooldownOverride = false;
            _cancelledInPreCast = false;
            _preCastInProgress = cast;
            try
            {
                SpellData.CastOverrides.Apply(CastInfo);
                Script.OnSpellPreCast(CastInfo.Owner, this, castInfo.Targets[0].Unit, start, end);
            }
            catch(Exception e)
            {
                _logger.Error(null, e);
            }
            finally
            {
                _preCastInProgress = false;
            }
            if (_cancelledInPreCast) return false;

            if (cast)
            {
                if (!CastInfo.IsAutoAttack && _game.Config.GameFeatures.HasFlag(FeatureFlags.EnableManaCosts))
                {
                    stats.CurrentMana -= GetManaCost() * (1 - stats.SpellCostReduction);
                }
            }

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
                if (Script.ScriptMetadata.TriggersSpellCasts || SpellData.ChannelDuration[CastInfo.SpellLevel] > 0 || Script.ScriptMetadata.ChannelDuration > 0)
                {
                    if (!SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                    {
                        if (!SpellData.CanMoveWhileChanneling && CastInfo.Owner.MovementParameters == null)
                        {
                            //CastInfo.Owner.StopMovement(networked:false);
                            // TODO: Verify if we should move this outside of this TriggersSpellCasts if statement.
                            CastInfo.Owner.UpdateMoveOrder(OrderType.CastSpell, true);
                        }
                    }

                    if (Script.ScriptMetadata.AutoFaceDirection)
                    {
                        var goingTo = end - CastInfo.Owner.Position;

                        if (CastInfo.Targets[0].Unit != null)
                        {
                            goingTo = CastInfo.Targets[0].Unit.Position - CastInfo.Owner.Position;
                        }

                        if (goingTo.LengthSquared() > 0)
                        {
                            var dirTemp = Vector2.Normalize(goingTo);
                            CastInfo.Owner.FaceDirection(new Vector3(dirTemp.X, 0, dirTemp.Y), false);
                        }
                    }
                }
            }

            if (CastInfo.IsAutoAttack && CastInfo.Owner.IsMelee)
            {
                _attackType = AttackType.ATTACK_TYPE_MELEE;
            }

            if (cast && CastInfo.Targets[0].Unit != null && CastInfo.Targets[0].Unit != CastInfo.Owner)
            {
                if (Script.ScriptMetadata.AutoFaceDirection)
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

                SetAttackWindupTiming();

                // TODO: Verify if this should be affected by cast variable.
                if (CastInfo.IsAutoAttack && IsBasicAttackSlotSpell())
                {
                    if (!CastInfo.IsSecondAutoAttack)
                    {
                        _game.PacketNotifier.NotifyBasic_Attack_Pos(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit);
                    }
                    else
                    {
                        _game.PacketNotifier.NotifyBasic_Attack(CastInfo.Owner, CastInfo.Targets[0].Unit, CastInfo.MissileNetID, CastInfo.Owner.IsNextAutoCrit, CastInfo.Owner.HasMadeInitialAttack);
                    }
                }

                if (CastInfo.UseAttackCastTime && !(CastInfo.IsAutoAttack && IsBasicAttackSlotSpell()))
                {
                    // TODO: Verify
                    CastInfo.DesignerTotalTime = CastInfo.DesignerCastTime + trueChannel;
                }
            }

            if (cast && CastInfo.Targets[0].Unit != null && CastInfo.Targets[0].Unit != CastInfo.Owner)
            {
                _game.PacketNotifier.NotifyS2C_UnitSetLookAt(CastInfo.Owner, CastInfo.Targets[0].Unit, _attackType);
            }

            if (cast)
            {
                if (!CastInfo.IsAutoAttack)
                {
                    if (SpellData.MaxAmmo > 1)
                    {
                        CurrentAmmo--;
                    }
                    Vector3 originalTarget = CastInfo.TargetPosition;
                    Vector3 originalTargetEnd = CastInfo.TargetPositionEnd;

                    if (!Script.ScriptMetadata.AutoFaceDirection)
                    {
                        var owner = CastInfo.Owner;
                        var forwardVec = GetSafeFacingDirection();

                        var fakePos = owner.Position + (forwardVec * 500.0f);
                        var fakePos3D = new Vector3(fakePos.X, _game.Map.NavigationGrid.GetHeightAtLocation(fakePos), fakePos.Y);

                        CastInfo.TargetPosition = fakePos3D;
                        CastInfo.TargetPositionEnd = fakePos3D;
                    }
                    try
                    {
                        _game.PacketNotifier.NotifyNPC_CastSpellAns(this);
                    }
                    finally
                    {
                        CastInfo.TargetPosition = originalTarget;
                        CastInfo.TargetPositionEnd = originalTargetEnd;
                    }
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

                return BeginCasting();
            }
            else
            {
                if (Script.ScriptMetadata.MissileParameters != null)
                {
                    CreateSpellMissile(Script.ScriptMetadata.MissileParameters, useClientCastVisual: false);
                }

                if (Script.ScriptMetadata.SectorParameters != null)
                {
                    CreateSpellSector();
                }
            }

            return true;
        }

        private bool BeginCasting()
        {
            uint castVersion = unchecked(++_castVersion);
            State = SpellState.STATE_CASTING;
            CurrentCastTime = !CastInfo.IsAutoAttack && SpellData.Flags.HasFlag(SpellDataFlags.InstantCast)
                ? 0 : CastInfo.DesignerCastTime;
            CurrentDelayTime = 0;

            // Network inputs arrive between updates. The next diff also includes
            // time before this cast began, which must not shorten its windup.
            _castTimeBeforeStart = _game.GetPendingSimulationMilliseconds();

            if (Script.ScriptMetadata.TriggersSpellCasts)
            {
                if (!CastInfo.IsAutoAttack && CastInfo.SpellSlot < 4)
                    ApiEventManager.OnAbilityCast.Publish(CastInfo.Owner, this);
                if (castVersion != _castVersion) return false;
                ApiEventManager.OnSpellCast.Publish(this);
            }
            if (castVersion != _castVersion)
            {
                return false;
            }
            if (CastInfo.IsAutoAttack)
            {
                ApiEventManager.OnPreAttack.Publish(CastInfo.Owner, this);
            }

            if (castVersion != _castVersion || _cancelledInPreCast || State != SpellState.STATE_CASTING)
            {
                return false;
            }
            if (CurrentCastTime <= 0)
            {
                CompleteCastAndStartChannel();
            }
            return true;
        }

        private Vector2 GetSafeFacingDirection()
        {
            var direction = new Vector2(CastInfo.Owner.Direction.X, CastInfo.Owner.Direction.Z);
            return float.IsFinite(direction.X) && float.IsFinite(direction.Y) && direction.LengthSquared() > 0
                ? Vector2.Normalize(direction) : Vector2.UnitX;
        }

        private void CompleteCastAndStartChannel()
        {
            uint castVersion = _castVersion;
            FinishCasting();
            if (castVersion != _castVersion)
            {
                return;
            }
            if (!_cancelledInPreCast && State == SpellState.STATE_CASTING
                && (SpellData.ChannelDuration[CastInfo.SpellLevel] > 0 || Script.ScriptMetadata.ChannelDuration > 0))
            {
                if (CastInfo.Owner.GetCastSpell() == null && CastInfo.Owner.ChannelSpell == null)
                {
                    Channel();
                }
                else
                {
                    BeginCooldown();
                }
            }
        }

        private void BeginCooldown()
        {
            if (!_hasActiveCooldownOverride)
            {
                CurrentCooldown = Math.Max(0, GetCooldown());
                CooldownTotalTime = CurrentCooldown;
                SetPassiveCooldownStats(CurrentCooldown);
                if (CastInfo.SpellSlot < 4)
                {
                    _game.PacketNotifier.NotifyCHAR_SetCooldown(CastInfo.Owner, CastInfo.SpellSlot, CurrentCooldown, GetCooldown());
                }
            }
            State = CurrentCooldown > 0 ? SpellState.STATE_COOLDOWN : SpellState.STATE_READY;
            _hasActiveCooldownOverride = false;
        }

        public void Channel()
        {
            State = SpellState.STATE_CHANNELING;
            _castTimeBeforeStart = _game.GetPendingSimulationMilliseconds();
            CurrentChannelDuration = SpellData.ChannelDuration[CastInfo.SpellLevel];

            if (Script.ScriptMetadata.ChannelDuration > 0)
            {
                CurrentChannelDuration = Script.ScriptMetadata.ChannelDuration;
            }

            if (CurrentChannelDuration > 0)
            {
                CastInfo.Owner.SetChannelSpell(this);
            }

            if (!SpellData.CanMoveWhileChanneling)
            {
                CastInfo.Owner.StopMovement();
            }

            ApiEventManager.OnSpellChannel.Publish(this);
        }

        public void ChannelCancelCheck()
        {
            if (CastInfo.Owner.IsDead && !SpellData.CanOnlyCastWhileDead)
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

            // TODO: Verify if Taunted should be handled by the Taunt buff script instead.
            var status = CastInfo.Owner.Status;
            if (status.HasFlag(StatusFlags.Charmed)
            || status.HasFlag(StatusFlags.Feared)
            || status.HasFlag(StatusFlags.Silenced)
            || status.HasFlag(StatusFlags.Stunned)
            || status.HasFlag(StatusFlags.Suppressed)
            || status.HasFlag(StatusFlags.Sleep)
            || status.HasFlag(StatusFlags.Taunted))
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

                bool canAffectUntargetable = SpellData.Flags.HasFlag(SpellDataFlags.AffectUntargetable);
                bool canAffectDead = SpellData.Flags.HasFlag(SpellDataFlags.AffectDead);
                if (spellTarget != CastInfo.Owner
                    && (!spellTarget.IsVisibleByTeam(CastInfo.Owner.Team)
                    || (!canAffectUntargetable && (!spellTarget.Status.HasFlag(StatusFlags.Targetable)
                        || !spellTarget.GetIsTargetableToTeam(CastInfo.Owner.Team)) && !spellTarget.CharData.IsUseable)
                    || (!canAffectDead && spellTarget.IsDead)))
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
            if (State != SpellState.STATE_CHANNELING && CastInfo.Owner.ChannelSpell != this)
            {
                return;
            }

            if (CastInfo.Owner.ChannelSpell == this)
            {
                CastInfo.Owner.SetChannelSpell(null);
            }

            BeginCooldown();
            uint castVersion = _castVersion;

            if (condition == ChannelingStopCondition.Cancel)
            {
                // For some reason this is the packet used for manually cancelling channels.
                _game.PacketNotifier.NotifyNPC_InstantStop_Attack(CastInfo.Owner, false);
                if (reason != ChannelingStopSource.TimeCompleted)
                {
                    ApiEventManager.OnSpellChannelCancel.Publish(this, reason);
                }
                if (castVersion != _castVersion)
                {
                    return;
                }

                var status = CastInfo.Owner.Status;
                bool isForcedMovement = status.HasFlag(StatusFlags.Feared) ||
                                        status.HasFlag(StatusFlags.Charmed) ||
                                        status.HasFlag(StatusFlags.Taunted);

                if (CastInfo.Owner.GetCastSpell() == null && CastInfo.Owner.ChannelSpell == null
                    && reason != ChannelingStopSource.Move && reason != ChannelingStopSource.Attack &&
                    !isForcedMovement)
                {
                    if (!SpellData.CanMoveWhileChanneling)
                    {
                        if (!CastInfo.Owner.IsPathEnded())
                        {
                            CastInfo.Owner.UpdateMoveOrder(OrderType.MoveTo, true, cancelQueuedSpell: false);
                            ApiFunctionManager.NotifyWaypointGroup(CastInfo.Owner);
                        }
                        else
                        {
                            CastInfo.Owner.UpdateMoveOrder(OrderType.Hold, true, cancelQueuedSpell: false);
                        }
                    }
                }
                // TODO: Find out how League calculates cooldown reduction for incomplete channels (assuming it isn't done in-script).
            }

            if (reason == ChannelingStopSource.TimeCompleted)
            {
                FinishChanneling();
            }
        }

        public void FinishCasting()
        {
            uint castVersion = _castVersion;
            if (CastInfo.IsAutoAttack)
            {
                ApiEventManager.OnLaunchAttack.Publish(CastInfo.Owner, this);
            }
            if (castVersion != _castVersion)
            {
                return;
            }

            if (CastInfo.IsAutoAttack)
            {
                CastInfo.Owner.HasAutoAttacked = true;
                bool shouldAdvanceBasicAttackChain = CastInfo.IsAutoAttack && IsBasicAttackSlotSpell();
                if (shouldAdvanceBasicAttackChain && !CastInfo.Owner.HasMadeInitialAttack)
                {
                    CastInfo.Owner.HasMadeInitialAttack = true;
                }
                bool resolveAttackOnCast = CastInfo.Owner.IsMelee
                    || (HasEmptyScript && CastInfo.IsAutoAttack && IsBasicAttackSlotSpell()
                        && SpellData.MissileSpeed <= 0.0f);
                if (!resolveAttackOnCast)
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
                    if (castVersion != _castVersion)
                    {
                        return;
                    }
                    CastInfo.Owner.AutoAttackHit(CastInfo.Targets[0].Unit);
                }
                if (castVersion != _castVersion)
                {
                    return;
                }

                State = SpellState.STATE_READY;
            }
            else
            {
                if (SpellData.ChannelDuration[CastInfo.SpellLevel] <= 0 && Script.ScriptMetadata.ChannelDuration <= 0)
                {
                    BeginCooldown();
                }
            }

            if (Script.ScriptMetadata.MissileParameters != null)
            {
                CreateSpellMissile();
            }
            if (castVersion != _castVersion)
            {
                return;
            }

            if (Script.ScriptMetadata.SectorParameters != null)
            {
                CreateSpellSector();
            }
            if (castVersion != _castVersion)
            {
                return;
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
            if (castVersion != _castVersion)
            {
                return;
            }
            if (CastInfo.Owner.GetCastSpell() != null || CastInfo.Owner.ChannelSpell != null)
            {
                return;
            }
            if (SpellData.Flags.HasFlag(SpellDataFlags.InstantCast) && CastInfo.Owner.IsAttacking)
            {
                return;
            }
            if (CastInfo.Owner.MovementParameters == null)
            {
                if (SpellData.ChannelDuration[CastInfo.SpellLevel] <= 0 && Script.ScriptMetadata.ChannelDuration <= 0)
                {
                    CastInfo.Owner.ResumePostponedMovementOrder();
                    shouldResumeWalking = !CastInfo.Owner.IsPathEnded();
                }
                if (SpellData.Flags.HasFlag(SpellDataFlags.InstantCast))
                {
                    if (shouldResumeWalking)
                    {
                        CastInfo.Owner.UpdateMoveOrder(OrderType.MoveTo, true, cancelQueuedSpell: false);
                        ApiFunctionManager.NotifyWaypointGroup(CastInfo.Owner);
                    }
                    else if (CastInfo.Owner.TargetUnit != null)
                    {
                        CastInfo.Owner.UpdateMoveOrder(OrderType.AttackTo, true, cancelQueuedSpell: false);
                    }
                }
                else
                {
                    bool willChannel = SpellData.ChannelDuration[CastInfo.SpellLevel] > 0 || Script.ScriptMetadata.ChannelDuration > 0;

                    if (!willChannel)
                    {
                        if (shouldResumeWalking)
                        {
                            CastInfo.Owner.UpdateMoveOrder(OrderType.MoveTo, true, cancelQueuedSpell: false);
                            ApiFunctionManager.NotifyWaypointGroup(CastInfo.Owner);
                        }
                        else
                        {
                            CastInfo.Owner.UpdateMoveOrder(OrderType.Hold, true, cancelQueuedSpell: false);
                        }
                    }
                    else
                    {
                        if (!SpellData.CanMoveWhileChanneling)
                        {
                            CastInfo.Owner.StopMovement();
                            if (!shouldResumeWalking)
                            {
                                CastInfo.Owner.UpdateMoveOrder(OrderType.Hold, true, cancelQueuedSpell: false);
                            }
                        }
                    }
                }
            }
        }


        public void FinishChanneling()
        {
            uint castVersion = _castVersion;
            if (CastInfo.Owner.ChannelSpell == this)
            {
                CastInfo.Owner.SetChannelSpell(null);
            }

            if (State == SpellState.STATE_CHANNELING)
            {
                BeginCooldown();
            }

            bool shouldResumeWalking = !CastInfo.Owner.IsPathEnded();

            ApiEventManager.OnSpellPostChannel.Publish(this);
            if (castVersion != _castVersion)
            {
                return;
            }
            if (CastInfo.Owner.MovementParameters == null
                && CastInfo.Owner.GetCastSpell() == null && CastInfo.Owner.ChannelSpell == null)
            {
                CastInfo.Owner.ResumePostponedMovementOrder();
                shouldResumeWalking = !CastInfo.Owner.IsPathEnded();
                if (shouldResumeWalking)
                {
                    CastInfo.Owner.UpdateMoveOrder(OrderType.MoveTo, true, cancelQueuedSpell: false);
                    ApiFunctionManager.NotifyWaypointGroup(CastInfo.Owner);
                }
                else
                {
                    CastInfo.Owner.UpdateMoveOrder(OrderType.Hold, true, cancelQueuedSpell: false);
                }
            }

        }

        public void Deactivate()
        {
            _cancelledInPreCast = true;
            unchecked 
            { 
                _castVersion++; 
            }
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
        public SpellMissile CreateSpellMissile(MissileParameters parameters, bool useClientCastVisual = true)
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

            castInfoClone.MissileNetID = netId;

            switch (parameters.Type)
            {
                case MissileType.Target:
                    {
                        p = new SpellMissile(
                            _game,
                            (int)SpellData.LineWidth,
                            this,
                            castInfoClone,
                            SpellData.MissileSpeed,
                            SpellData.Flags,
                            netId,
                            isServerOnly
                        );
                        break;
                    }
                case MissileType.Chained:
                    {
                        p = new SpellChainMissile(
                            _game,
                            (int)SpellData.LineWidth,
                            this,
                            castInfoClone,
                            parameters,
                            SpellData.MissileSpeed,
                            SpellData.Flags,
                            netId,
                            isServerOnly
                        );
                        break;
                    }
                case MissileType.Circle:
                    {
                        p = new SpellCircleMissile(
                            _game,
                            (int)SpellData.LineWidth,
                            this,
                            castInfoClone,
                            SpellData.MissileSpeed,
                            parameters.OverrideEndPosition,
                            SpellData.Flags,
                            netId,
                            isServerOnly
                        );
                        break;
                    }
                case MissileType.Arc:
                    {
                        p = new SpellLineMissile(
                            _game,
                            (int)SpellData.LineWidth,
                            this,
                            castInfoClone,
                            SpellData.MissileSpeed,
                            parameters.OverrideEndPosition,
                            SpellData.Flags,
                            netId,
                            isServerOnly
                        );
                        break;
                    }
            }

            // If the position is the same as the destination, the server will have destroyed the missile before notifying of creation, causing the client to crash.
            // TODO: Make a better check.
            if (p == null || (p is SpellCircleMissile c && c.Position == c.Destination)
                || p.Position == p.GetTargetPosition())
            {
                return null;
            }

            InitializeVisualMissile(p, parameters);
            p.UsesClientCastVisual = useClientCastVisual && netId == CastInfo.MissileNetID
                && !p.IsVisualOnly && p.ReplicationHeightOffset == null
                && (castInfoClone.IsAutoAttack
                    || (isServerOnly && SpellData.CastType != (int)CastType.CAST_Instant));
            var direction = castInfoClone.TargetPositionEnd - castInfoClone.SpellCastLaunchPosition;
            if (direction.LengthSquared() > .001f)
                p.FaceDirection(Vector3.Normalize(direction));

            p.HasExplicitLaunchReplication = p.UsesClientCastVisual && parameters.ReplicateOnLaunch
                && !castInfoClone.IsAutoAttack;

            p.IsInInitialSpawn = true;
            try
            {
                _game.ObjectManager.AddObject(p);
            }
            finally
            {
                p.IsInInitialSpawn = false;
            }

            if (p.UsesClientCastVisual)
            {
                if (parameters.ReplicateOnLaunch)
                {
                    _game.PacketNotifier.NotifyMissileLaunch(p);
                }
                _game.PacketNotifier.NotifyForceCreateMissile(p);
            }
            ApiEventManager.OnLaunchMissile.Publish(this, p);

            return p;
        }

        /// <summary>
        /// Creates a spell missile using this spell's script for the parameters.
        /// </summary>
        public SpellMissile CreateSpellMissile()
        {
            return CreateSpellMissile(Script.ScriptMetadata.MissileParameters);
        }

        private void InitializeVisualMissile(SpellMissile missile, MissileParameters parameters)
        {
            missile.IsVisualOnly = parameters.IsVisualOnly;
            missile.ReplicationHeightOffset = parameters.ReplicationHeightOffset;
            missile.ReplicationEndHeightOffset = parameters.ReplicationEndHeightOffset;
            if (!missile.IsVisualOnly) return;
            if (missile.TargetUnit != null)
            {
                missile.CastInfo.TargetPosition = missile.TargetUnit.GetPosition3D();
                missile.CastInfo.TargetPositionEnd = missile.TargetUnit.GetPosition3D();
            }
            var direction = missile.CastInfo.TargetPositionEnd - missile.CastInfo.SpellCastLaunchPosition;
            direction.Y += SpellData.MissileTargetHeightAugment;
            if (direction.LengthSquared() > .001f) missile.FaceDirection(Vector3.Normalize(direction));
        }

        /// <summary>
        /// Creates a spell sector with the given parameters.
        /// </summary>
        /// <param name="parameters">Parameters of the sector.</param>
        public SpellSector CreateSpellSector(SectorParameters parameters)
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

            SpellSector s = null;
            var castInfoClone = CastInfo.Clone();

            switch (parameters.Type)
            {
                case SectorType.Area:
                    {
                        s = new SpellSector(
                            _game,
                            parameters,
                            this,
                            castInfoClone,
                            netId
                        );
                        break;
                    }
                case SectorType.Cone:
                    {
                        s = new SpellSectorCone(
                            _game,
                            parameters,
                            this,
                            castInfoClone,
                            netId
                        );
                        break;
                    }
                case SectorType.Polygon:
                    {
                        s = new SpellSectorPolygon(
                            _game,
                            parameters,
                            this,
                            castInfoClone,
                            netId
                        );
                        break;
                    }
                case SectorType.Ring:
                    {
                        // TODO
                        break;
                    }
            }

            ApiEventManager.OnCreateSector.Publish(this, s);
            _game.ObjectManager.AddObject(s);

            // TODO: Verify when NotifyForceCreateMissile should be used instead.

            return s;
        }

        /// <summary>
        /// Creates a spell sector using this spell's script for the parameters.
        /// </summary>
        public SpellSector CreateSpellSector()
        {
            return CreateSpellSector(Script.ScriptMetadata.SectorParameters);
        }

        private float GetCooldownReductionMultiplier()
        {
            return Math.Max(0.0f, 1.0f + CastInfo.Owner.Stats.CooldownReduction.Total);
        }

        public float GetCooldown(byte? rank = null)
        {
            if (_game.Config.GameFeatures.HasFlag(FeatureFlags.EnableCooldowns))
            {
                if (SpellData.Cooldown == null || SpellData.Cooldown.Length == 0)
                {
                    return 0.0f;
                }

                var level = Math.Clamp(rank ?? CastInfo.SpellLevel, (byte)0, (byte)(SpellData.Cooldown.Length - 1));
                var cd = Math.Max(0, Script?.GetCooldown(this, level) ?? SpellData.Cooldown[level]);
                if (Script?.ScriptMetadata?.CooldownIsAffectedByCDR == true && CastInfo.SpellSlot < 4)
                {
                    cd *= GetCooldownReductionMultiplier();
                }
                if ((SpellName == "SummonerFlash" || SpellName == "SummonerHaste" || SpellName == "SummonerTeleport")
                    && CastInfo.Owner.GetBuffWithName("ItemBootEffects")?.Variables.GetBool("distortion") == true)
                {
                    cd *= 0.8f;
                }
                return cd;
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

            if (Script?.ScriptMetadata?.CooldownIsAffectedByCDR == true && CastInfo.SpellSlot < 4)
            {
                cd *= GetCooldownReductionMultiplier();
            }

            return cd;
        }

        public void AddAmmo(int ammount = 1)
        {
            CurrentAmmo = Math.Min(CurrentAmmo + ammount, SpellData.MaxAmmo);
            CurrentAmmoCooldown = GetAmmoRechageTime();


            _game.PacketNotifier.NotifyS2C_AmmoUpdate(this);
        }

        public float GetCurrentCastRange()
        {
            if (_overrrideCastRange > 0)
            {
                return _overrrideCastRange;
            }

            var ranges = SpellData.CastRange;
            if (ranges == null || ranges.Length == 0)
            {
                return 0;
            }
            int rank = Math.Clamp((int)CastInfo.SpellLevel, 0, ranges.Length - 1);
            return ranges[rank];
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
                CastInfo.Owner.Stats.ManaCost[CastInfo.SpellSlot] = GetManaCost();
            }

            if (CastInfo.SpellLevel == 1 && SpellData.MaxAmmo > 1)
            {
                CurrentAmmo = SpellData.MaxAmmo; // Or whatever the starting ammo should be so keep in mind if it breaks
                CurrentAmmoCooldown = GetAmmoRechageTime();
                _game.PacketNotifier.NotifyS2C_AmmoUpdate(this);
            }
        }

        public void LowerCooldown(float lowerValue)
        {
            SetCooldown(CurrentCooldown - lowerValue, true, CooldownTotalTime);
        }

        public void ResetSpellCast()
        {
            State = SpellState.STATE_READY;
            CurrentCastTime = 0;
            CurrentChannelDuration = 0;
            CurrentDelayTime = 0;
        }

        /// <summary>
        /// Adds the specified unit to the list of targets for this spell.
        /// </summary>
        /// <param name="target">Unit to remove.</param>
        public void AddTarget(AttackableUnit target)
        {
            CastInfo.AddTarget(target);

            if ((State == SpellState.STATE_CASTING || State == SpellState.STATE_CHANNELING)
                && CastInfo.Targets.Count == 1 && CastInfo.Targets[0].Unit != null)
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
            if (target != null && target != CastInfo.Owner
                && (CastInfo.Targets.Count == 0 || CastInfo.Targets[0].Unit != target))
            {
                CastInfo.SetTarget(target, 0);
                if (State == SpellState.STATE_CASTING || State == SpellState.STATE_CHANNELING)
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
        /// Overrides the normal cast range for this spell. Set to 0 to revert.
        /// </summary>
        /// <param name="newCastRange">Cast range to set.</param>
        public void SetOverrideCastRange(float newCastRange)
        {
            _overrrideCastRange = newCastRange;

            if (CastInfo.Owner is Champion champion)
            {
                _game.PacketNotifier.NotifyChangeSlotSpellData
                (
                    _game.PlayerManager.GetClientInfoByChampion(champion).ClientId,
                    champion,
                    (byte)CastInfo.SpellSlot,
                    ChangeSlotSpellDataType.Range,
                    newRange: newCastRange
                );
            }
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
        public void SetCooldown(float newCd, bool ignoreCDR = false, float? totalCooldown = null)
        {
            bool active = _preCastInProgress || State == SpellState.STATE_CASTING || State == SpellState.STATE_CHANNELING;
            if (active)
            {
                _hasActiveCooldownOverride = true;
            }
            if (newCd <= 0)
            {
                _game.PacketNotifier.NotifyCHAR_SetCooldown(CastInfo.Owner, CastInfo.SpellSlot, 0, 0);
                SetPassiveCooldownStats(0);
                CurrentCooldown = 0;
                CooldownTotalTime = 0;
                // Changing the state of the spell to READY during casting prevents the spell from finishing casting, thus locking the player in the move order CastSpell.
                if (!active)
                {
                    State = SpellState.STATE_READY;
                    CurrentCooldown = 0;
                }
            }
            else
            {
                if (!ignoreCDR && Script?.ScriptMetadata?.CooldownIsAffectedByCDR == true && CastInfo.SpellSlot < 4)
                {
                    newCd *= GetCooldownReductionMultiplier();
                }

                CooldownTotalTime = Math.Max(newCd, totalCooldown ?? newCd);
                _game.PacketNotifier.NotifyCHAR_SetCooldown(CastInfo.Owner, CastInfo.SpellSlot, newCd, CooldownTotalTime);
                if (!active)
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
                CastInfo.Owner.Stats.ManaCost[CastInfo.SpellSlot] = GetManaCost();
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

        public void SetIconIndex(byte iconIndex)
        {
            if (IconIndex == iconIndex) return;
            IconIndex = iconIndex;
            if (CastInfo.Owner is Champion champion
                && _game.PlayerManager.GetClientInfoByChampion(champion) is { } client)
                _game.PacketNotifier.NotifySpellIcon(client.ClientId, this);
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

            CastInfo.TargetPosition = position;
            CastInfo.TargetPositionEnd = position;

            Script.OnSpellChannelUpdate(this, position, forceStop);

            if (forceStop)
            {
                StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.PlayerCommand);
            }
        }
        public SpellMissile CreateCustomMissile(Vector2 start, Vector2 end, MissileParameters parameters, bool isForceCastingOrChannel = false, bool isOverrideCastPosition = true, float? customHeightOffset = null, AttackableUnit target = null)
        {
            var netId = _networkIdManager.GetNewNetId();
            var castInfoClone = CastInfo.Clone();

            SpellData.CastOverrides.Apply(castInfoClone);
            if (castInfoClone.SpellNetID == 0)
                castInfoClone.SpellNetID = _networkIdManager.GetNewNetId();
            if (castInfoClone.SpellChainOwnerNetID == 0)
                castInfoClone.SpellChainOwnerNetID = CastInfo.Owner.NetId;

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

            if (parameters.LaunchHeight is MissileHeight launchHeight)
            {
                float groundHeight = _game.Map.NavigationGrid.GetHeightAtLocation(start);
                castInfoClone.SpellCastLaunchPosition = new Vector3(start.X, launchHeight.Resolve(groundHeight), start.Y);
            }

            if (parameters.DestinationHeight is MissileHeight destinationHeight)
            {
                float groundHeight = _game.Map.NavigationGrid.GetHeightAtLocation(end);
                float height = destinationHeight.Resolve(groundHeight);
                castInfoClone.TargetPosition = new Vector3(end.X, height, end.Y);
                castInfoClone.TargetPositionEnd = new Vector3(end.X, height, end.Y);
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

            bool isServerOnly = SpellData.MissileEffect != "";

            SpellMissile p = null;

            switch (parameters.Type)
            {
                case MissileType.Target:
                    p = new SpellMissile(_game, (int)SpellData.LineWidth, this, castInfoClone, SpellData.MissileSpeed, SpellData.Flags, netId, isServerOnly);
                    break;
                case MissileType.Chained:
                    p = new SpellChainMissile(_game, (int)SpellData.LineWidth, this, castInfoClone, parameters, SpellData.MissileSpeed, SpellData.Flags, netId, isServerOnly);
                    break;
                case MissileType.Circle:
                    p = new SpellCircleMissile(_game, (int)SpellData.LineWidth, this, castInfoClone, SpellData.MissileSpeed, end, SpellData.Flags, netId, isServerOnly);
                    break;
                case MissileType.Arc:
                    p = new SpellLineMissile(_game, (int)SpellData.LineWidth, this, castInfoClone, SpellData.MissileSpeed, end, SpellData.Flags, netId, isServerOnly);
                    break;
            }

            if (p == null || p.Position == p.GetTargetPosition())//if (p == null || (p is SpellCircleMissile c && c.Position == c.Destination) || p.Position == p.GetTargetPosition())
            {
                return null;
            }

            InitializeVisualMissile(p, parameters);
            var launchDirection = castInfoClone.TargetPositionEnd - castInfoClone.SpellCastLaunchPosition;
            p.HasCustomHeightProfile = parameters.LaunchHeight.HasValue || parameters.DestinationHeight.HasValue;
            if ((!p.IsVisualOnly || p.HasCustomHeightProfile) && launchDirection.LengthSquared() > .001f)
                p.FaceDirection(Vector3.Normalize(launchDirection));
            _game.ObjectManager.AddObject(p);
            ApiEventManager.OnLaunchMissile.Publish(this, p);

            return p;
        }
        /// <summary>
        /// Called every diff milliseconds to update the spell
        /// </summary>
        public void Update(float diff)
        {
            UpdateFromOwner(diff, _castVersion);
        }

        internal uint GetCastVersion()
        {
            return _castVersion;
        }

        internal void UpdateFromOwner(float diff, uint? castVersion)
        {
            if (!HasEmptyScript)
            {
                try
                {
                    Script.OnUpdate(diff);
                }
                catch(Exception e)
                {
                    _logger.Error(null, e);
                }
            }

            // A cast started by an update callback has not elapsed through that update.
            float castDiff = castVersion == _castVersion ? diff : 0;
            if (castVersion == _castVersion)
            {
                castDiff = Math.Max(0, diff - _castTimeBeforeStart);
                _castTimeBeforeStart = Math.Max(0, _castTimeBeforeStart - diff);
            }

            if (State == SpellState.STATE_COOLDOWN || _hasActiveCooldownOverride)
            {
                CurrentCooldown = Math.Max(0, CurrentCooldown - diff / 1000.0f);
                if (State == SpellState.STATE_COOLDOWN && CurrentCooldown <= 0)
                {
                    State = SpellState.STATE_READY;
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
                            CurrentCastTime -= castDiff / 1000.0f;
                            if (CurrentCastTime <= 0)
                            {
                                CompleteCastAndStartChannel();
                            }
                        }
                        else
                        {
                            CurrentDelayTime += castDiff / 1000.0f;
                            if (CurrentDelayTime >= CastInfo.DesignerCastTime / CastInfo.AttackSpeedModifier)
                            {
                                CompleteCastAndStartChannel();
                            }
                        }
                        break;
                    }
                case SpellState.STATE_COOLDOWN:
                    {
                        break;
                    }
                case SpellState.STATE_CHANNELING:
                    {
                        CurrentChannelDuration -= castDiff / 1000.0f;
                        ChannelCancelCheck();
                        if (State == SpellState.STATE_CHANNELING && CurrentChannelDuration <= 0)
                        {
                            StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.TimeCompleted);//FinishChanneling();
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
        public void CancelCast()
        {
            unchecked 
            { 
                _castVersion++; 
            }
            _cancelledInPreCast = true;
            if (CastInfo.IsAutoAttack)
            {
                CastInfo.Owner.CancelAutoAttack(true, true);
            }
            else
            {
                bool wasCasting = State == SpellState.STATE_CASTING;
                bool ownsCast = CastInfo.Owner.GetCastSpell() == this;
                bool keepCooldown = _hasActiveCooldownOverride && CurrentCooldown > 0;
                ResetSpellCast();
                _hasActiveCooldownOverride = false;
                if (keepCooldown)
                {
                    State = SpellState.STATE_COOLDOWN;
                }
                if (ownsCast)
                {
                    CastInfo.Owner.SetCastSpell(null);
                }
                if (ownsCast || (wasCasting && CastInfo.Owner.GetCastSpell() == null && CastInfo.Owner.ChannelSpell == null))
                {
                    _game.PacketNotifier.NotifyNPC_InstantStop_Attack(CastInfo.Owner, false);
                }
            }
        }
    }
}
