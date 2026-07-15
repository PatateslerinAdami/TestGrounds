using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.AreaTriggers;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net;
using PacketDefinitions420;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using GameMaths;
using GameMaths.Geometry.Polygons;
using GameServerCore;

namespace LeagueSandbox.GameServer.API
{
    /// <summary>
    /// Class housing functions most commonly used by scripts.
    /// </summary>
    public static class ApiFunctionManager
    {
        // Required variables.
        private static Game _game;
        private static ILog _logger = LoggerProvider.GetLogger();
        // RNG source for area/point helpers (BBGetRandomPointInArea*). Matches the per-class
        // "static readonly Random" convention used elsewhere in the server.
        private static readonly Random _random = new Random();

        /// <summary>
        /// Converts the given string of hex values into an array of bytes.
        /// </summary>
        /// <param name="hex">String of hex values.</param>
        /// <returns>Array of bytes.</returns>
        public static byte[] StringToByteArray(string hex)
        {
            hex = hex.Replace(" ", string.Empty);
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }

        /// <summary>
        /// Sets the Game instance of ApiFunctionManager to the given instance.
        /// Also assigns the debug logger.
        /// </summary>
        /// <param name="game">Game instance to set.</param>
        internal static void SetGame(Game game)
        {
            _game = game;
        }

        /// <summary>
        /// Kills a unit using the legacy NPC_Die_Broadcast (op 0x9E) packet instead of the
        /// 4.18+ S2C_NPC_Die_MapView (op 0x126) that AttackableUnit.Die() emits. Use this
        /// when a script needs the unit's NetId to remain resolvable on the client for a
        /// brief window after death (e.g. for an FX packet that references it via
        /// TargetNetID). Replay-verified pattern from Xerath Q chain anchors — Riot kills
        /// the chain ~60ms before the beam packet, and the client retains the dead NetIds
        /// in its object table just long enough for the .troy to resolve them. The newer
        /// S2C_NPC_Die_MapView path purges the NetId immediately, breaking that pattern.
        /// </summary>
        public static void KillUnitLegacyBroadcast(AttackableUnit unit)
        {
            var data = new GameServerLib.GameObjects.AttackableUnits.DeathData
            {
                Unit = unit,
                Killer = unit,
                DamageType = DamageType.DAMAGE_TYPE_TRUE,
                DamageSource = DamageSource.DAMAGE_SOURCE_INTERNALRAW,
            };
            _game.PacketNotifier.NotifyNPC_Die_Broadcast(data);
            unit.SetToRemove();
        }

        /// <summary>
        /// Pushes a model/skin override layer onto a unit (transform / object-data swap / evolving
        /// skin) and returns the layer id. Pass that id to <see cref="PopCharacterData"/> to revert
        /// exactly this layer; other layers and the base skin are untouched. The unit's server-side
        /// <c>Model</c>/<c>SkinID</c> follow the resolved top of the stack.
        /// </summary>
        /// <param name="unit">Unit to transform.</param>
        /// <param name="skinName">Internally named model/skin to display (e.g. "EliseSpider").</param>
        /// <param name="skinID">Skin index, or -1 to keep the unit's current skinID.</param>
        /// <param name="overrideSpells">Take the spellbook from this layer (Nidalee/Elise alt form).</param>
        /// <param name="modelOnly">Change the model only, leave the spellbook untouched.</param>
        /// <param name="replaceCharacterPackage">Replace the entire character package (rare).</param>
        /// <returns>The layer id, for a later <see cref="PopCharacterData"/>.</returns>
        public static uint PushCharacterData(AttackableUnit unit, string skinName, int skinID = -1,
            bool overrideSpells = false, bool modelOnly = false, bool replaceCharacterPackage = false)
        {
            return unit.CharacterDataStack.Push(skinName, skinID, overrideSpells, modelOnly, replaceCharacterPackage);
        }

        /// <summary>
        /// Removes the model/skin override layer with the given id (from <see cref="PushCharacterData"/>),
        /// reverting the unit to the layer beneath (or its base skin).
        /// </summary>
        public static void PopCharacterData(AttackableUnit unit, uint id)
        {
            unit.CharacterDataStack.PopSpecific(id);
        }

        /// <summary>Removes the topmost model/skin override layer from a unit (revert last transform).</summary>
        public static void PopCharacterData(AttackableUnit unit)
        {
            unit.CharacterDataStack.Pop();
        }

        /// <summary>Clears all model/skin override layers from a unit, reverting it to its base skin.</summary>
        public static void PopAllCharacterData(AttackableUnit unit)
        {
            unit.CharacterDataStack.PopAll();
        }

        /// <summary>
        /// Overrides a unit's voice-over bank on clients (S2C_ChangeCharacterVoice) — e.g. Riven "Ult"
        /// during her ultimate, Sion "Berserk"/"Max". Pair with <see cref="ResetCharacterVoiceOverride"/>
        /// to revert when the form/ult ends.
        /// </summary>
        public static void SetCharacterVoiceOverride(AttackableUnit unit, string voiceOverride)
        {
            _game.PacketNotifier.NotifyS2C_ChangeCharacterVoice(unit, voiceOverride, reset: false);
        }

        /// <summary>Resets a unit's voice-over bank back to its default (S2C_ChangeCharacterVoice reset flag).</summary>
        public static void ResetCharacterVoiceOverride(AttackableUnit unit)
        {
            _game.PacketNotifier.NotifyS2C_ChangeCharacterVoice(unit, "", reset: true);
        }

        /// <summary>
        /// Asks clients to preload a skin's assets so a later <see cref="PushCharacterData"/> swaps
        /// without a hitch. Broadcast to everyone (matches the client's broadcast policy).
        /// </summary>
        public static void PreloadCharacterData(AttackableUnit unit, string skinName, int skinID = -1)
        {
            _game.PacketNotifier.NotifyS2C_PreloadCharacterData(unit, skinName, skinID);
        }

        /// <summary>
        /// Registers a shop item substitution for the given champion and notifies its client: the shop
        /// shows/sells <paramref name="substitutionItemId"/> wherever <paramref name="originalItemId"/>
        /// would appear (S2C_ShopItemSubstitutionSet). E.g. the Culinary Master mastery swaps Health
        /// Potion (2003) for the Biscuit (2010). No-op for non-champions or zero ids. Call it from the
        /// granting talent/spell script (e.g. on activate). The mapping persists on the champion's shop
        /// (queryable via Shop.GetItemSubstitution).
        /// </summary>
        public static void SetShopItemSubstitution(ObjAIBase owner, int originalItemId, int substitutionItemId)
        {
            if (owner is Champion champion)
            {
                champion.Shop.SetItemSubstitution(originalItemId, substitutionItemId);
            }
        }

        /// <summary>
        /// Removes a shop item substitution for the given champion and notifies its client
        /// (S2C_ShopItemSubstitutionClear). NOTE: Riot does NOT send Clear in 4.20 (0× across 40 replays —
        /// substitutions just persist for the game), so a faithful Culinary-Master-style script does NOT
        /// need this. Provided for completeness / non-SR modes. No-op for non-champions.
        /// </summary>
        public static void ClearShopItemSubstitution(ObjAIBase owner, int originalItemId)
        {
            if (owner is Champion champion)
            {
                champion.Shop.ClearItemSubstitution(originalItemId);
            }
        }

        /// <summary>
        /// Logs the given string to the server console as info.
        /// </summary>
        /// <param name="format">String to print.</param>
        public static void LogInfo(string format)
        {
            _logger.Info(format);
        }

        /// <summary>
        /// Logs the given string and its arguments to the server console as info.
        /// Instanced classes in the arguments will be a string representation of the object's namespace.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void LogInfo(string format, params object[] args)
        {
            _logger.Info(string.Format(format, args));
        }

        /// <summary>
        /// Logs the given string to the server console as debug info.
        /// Only works in Debug mode.
        /// </summary>
        /// <param name="format">String to debug print.</param>
        public static void LogDebug(string format)
        {
            _logger.Debug(format);
        }

        /// <summary>
        /// Logs the given string to the server console as debug info.
        /// Only works in Debug mode.
        /// Instanced classes in the arguments will be a string representation of the object's namespace.
        /// </summary>
        /// <param name="format">String to debug print.</param>
        public static void LogDebug(string format, params object[] args)
        {
            _logger.Debug(string.Format(format, args));
        }

        /// <summary>
        /// Schedules a fire-once callback after <paramref name="duration"/> seconds, as a
        /// GAME-scoped <see cref="GameScriptTimer"/> — it ticks on the global game loop, so it
        /// still fires if the owner dies during the delay (e.g. a Vel'Koz W rift detonates even
        /// if Vel'Koz died). The delay is wall-clock-accumulated in seconds, matching Riot's
        /// time-based scripting model (BBExecutePeriodically). For a timer that should be cancelled
        /// when a specific unit dies, use <c>unit.RegisterTimer(new GameScriptTimer(...))</c> instead.
        /// Returns the timer so callers can query/cancel it (IsDead/EndTimerNow).
        /// </summary>
        /// <param name="duration">Delay in seconds before the callback fires.</param>
        /// <param name="callback">Action to perform when the timer ends.</param>
        /// <returns>The registered GameScriptTimer instance.</returns>
        public static GameScriptTimer CreateTimer(float duration, Action callback)
        {
            var newTimer = new GameScriptTimer(duration, callback);
            _game.AddGameScriptTimer(newTimer);

            return newTimer;
        }

        /// <summary>
        /// Sets the visibility of the specified GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject to set.</param>
        /// <param name="visibility">Whether or not the GameObject should be visible.</param>
        public static void SetGameObjectVisibility(GameObject gameObject, bool visibility)
        {
            var teams = GetTeams();
            foreach (var id in teams)
            {
                gameObject.SetVisibleByTeam(id, visibility);
            }
        }

        /// <summary>
        /// Gets the possible teams.
        /// </summary>
        /// <returns>Usually BLUE/PURPLE/NEUTRAL.</returns>
        public static List<TeamId> GetTeams()
        {
            return _game.ObjectManager.Teams;
        }

        public static int ConvertAPISlot(SpellSlotType slotType, int slot)
        {
            if ((slotType == SpellSlotType.SpellSlots && (slot < 0 || slot > 3))
                || (slotType == SpellSlotType.InventorySlots && (slot < 0 || slot > 6))
                || (slotType == SpellSlotType.ExtraSlots && (slot < 0 || slot > 15)))
            {
                return -1;
            }

            if (slotType == SpellSlotType.SummonerSpellSlots)
            {
                slot += (int)SpellSlotType.SummonerSpellSlots;
            }
            else if (slotType == SpellSlotType.InventorySlots)
            {
                slot += (int)SpellSlotType.InventorySlots;
            }
            else if (slotType == SpellSlotType.TempItemSlot)
            {
                slot = (int)SpellSlotType.TempItemSlot;
            }
            else if (slotType == SpellSlotType.ExtraSlots)
            {
                slot += (int)SpellSlotType.ExtraSlots;
            }

            return slot;
        }

        public static int ConvertAPISlot(SpellbookType spellbookType, SpellSlotType slotType, int slot)
        {
            if (spellbookType == SpellbookType.SPELLBOOK_UNKNOWN
                || spellbookType == SpellbookType.SPELLBOOK_SUMMONER && (slotType != SpellSlotType.SummonerSpellSlots)
                || (spellbookType == SpellbookType.SPELLBOOK_CHAMPION
                    && ((slotType == SpellSlotType.SpellSlots && (slot < 0 || slot > 3))
                        || (slotType == SpellSlotType.InventorySlots && (slot < 0 || slot > 6))
                        || (slotType == SpellSlotType.ExtraSlots && (slot < 0 || slot > 15)))))
            {
                return -1;
            }

            if (spellbookType == SpellbookType.SPELLBOOK_CHAMPION)
            {
                if (slotType == SpellSlotType.InventorySlots)
                {
                    slot += (int)SpellSlotType.InventorySlots;
                }
                else if (slotType == SpellSlotType.TempItemSlot)
                {
                    slot = (int)SpellSlotType.TempItemSlot;
                }
                else if (slotType == SpellSlotType.ExtraSlots)
                {
                    slot += (int)SpellSlotType.ExtraSlots;
                }
            }
            else if (spellbookType == SpellbookType.SPELLBOOK_SUMMONER)
            {
                if (slotType == SpellSlotType.SummonerSpellSlots)
                {
                    slot += (int)SpellSlotType.SummonerSpellSlots;
                }
            }

            return slot;
        }

        /// <summary>
        /// Converts a spell's absolute slot number to a 0-based inventory slot index.
        /// Returns -1 when the spell is null or does not belong to an inventory slot.
        /// </summary>
        /// <param name="spell">Spell to convert.</param>
        /// <returns>0-based inventory slot index, or -1 if invalid.</returns>
        public static int ToInventorySlotIndex(Spell spell)
        {
            if (spell == null)
            {
                return -1;
            }

            var inventorySlot = spell.CastInfo.SpellSlot - (int)SpellSlotType.InventorySlots;
            var maxInventorySlot = (int)SpellSlotType.BluePillSlot - (int)SpellSlotType.InventorySlots - 1;
            return inventorySlot >= 0 && inventorySlot <= maxInventorySlot ? inventorySlot : -1;
        }

        /// <summary>
        /// Teleports an AI unit to the specified coordinates.
        /// Instant.
        /// ObjAIBase is the correct scope — Riot's script teleport is obj_AI_Base-typed in both
        /// eras (LuaSpellScriptHelper::TeleportToPositionR3dPoint(obj_AI_Base*, r3dPoint3D), S1
        /// server + 4.17 client decomp), so this should NOT be widened to GameObject. Riot snaps
        /// the target to the nearest passable cell (our GetClosestTerrainExit inside TeleportTo)
        /// and refuses the teleport when the snapped target lies outside the unit's active
        /// circular movement restriction (IsGoalPosRestricted checks mMovementRestrictionCenter/
        /// Radius). We only have the wire side of that feature (S2C_SetCircularMovementRestriction),
        /// no server-side restriction state, so there is nothing to gate on yet.
        /// </summary>
        /// <param name="unit">AI unit to teleport.</param>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="silent">Passed through to AttackableUnit.TeleportTo: no movement-update
        /// flag and no networked StopMovement (for blink spells with their own position sync).</param>
        public static void TeleportTo(ObjAIBase unit, float x, float y, bool silent = false)
        {
            if (unit.MovementParameters != null)
            {
                CancelForceMovement(unit);
            }

            unit.TeleportTo(x, y, silent: silent);
        }

        public static void FaceDirection(Vector2 location, GameObject target, bool isInstant = false,
            float turnTime = 0.08333f)
        {
            if (location == target.Position)
            {
                return;
            }

            var goingTo = location - target.Position;
            var direction = Vector2.Normalize(goingTo);

            target.FaceDirection(new Vector3(direction.X, 0, direction.Y), isInstant, turnTime);
        }

        /// <summary>
        /// Faithful port of S1 <c>BBGetPointByUnitFacingOffset</c> (LuaBuildingBlockHelper.cpp): take
        /// the unit's facing direction, rotate it <paramref name="offsetAngle"/> degrees (clockwise)
        /// around the vertical axis, normalize, and step <paramref name="distance"/> units from the
        /// unit's position along it. Riot pairs this with a preceding BBFaceDirection
        /// (see <see cref="FaceDirection"/>) so the facing already points where the point is wanted.
        /// </summary>
        /// <param name="unit">Unit to base the point off of (uses its current facing).</param>
        /// <param name="distance">Distance from the unit along the (rotated) facing direction.</param>
        /// <param name="offsetAngle">Offset from the facing angle, in degrees clockwise (0 = straight ahead).</param>
        /// <returns>The resulting world point.</returns>
        public static Vector2 GetPointByUnitFacingOffset(GameObject unit, float distance, float offsetAngle = 0f)
        {
            Vector2 dir = new Vector2(unit.Direction.X, unit.Direction.Z);
            if (offsetAngle != 0f)
            {
                dir = GameServerCore.Extensions.Rotate(dir, offsetAngle);
            }

            // C++ normalizes the facing before scaling; Direction is normally unit-length already, so
            // this is a no-op guard unless the unit has never faced anything (Direction == Zero).
            if (dir != Vector2.Zero)
            {
                dir = Vector2.Normalize(dir);
            }

            return unit.Position + dir * distance;
        }

        /// <summary>
        /// Legacy alias for <see cref="GetPointByUnitFacingOffset"/> (identical behavior). Retained
        /// for the existing script call sites that use this name.
        /// </summary>
        public static Vector2 GetPointFromUnit(GameObject obj, float distance, float offsetAngle = 0)
        {
            return GetPointByUnitFacingOffset(obj, distance, offsetAngle);
        }

        /// <summary>
        /// Writes a value into the unit's persistent CharVars table (see
        /// <see cref="ObjAIBase.CharVars"/>). Script-facing analog of S1's <c>BBSetVarInTable</c>
        /// with <c>DestVarTable = "CharVars"</c>. Survives across casts/buffs; use for a champion's
        /// own state flags (e.g. "passive empowered"), not for per-cast or per-buff data.
        /// </summary>
        public static void SetCharVar(ObjAIBase unit, string name, object value)
        {
            unit.CharVars.Set(name, value);
        }

        /// <summary>
        /// Reads a typed value from the unit's persistent CharVars table, or
        /// <paramref name="defaultValue"/> if unset. Script-facing analog of reading a var with
        /// <c>SrcVarTable = "CharVars"</c>. Riot's flags are numeric (0/1) — read those as
        /// <c>GetCharVar&lt;int&gt;</c> or via the bag's own <c>GetBool</c>/<c>GetInt</c> helpers.
        /// </summary>
        public static T GetCharVar<T>(ObjAIBase unit, string name, T defaultValue = default)
        {
            return unit.CharVars.Get(name, defaultValue);
        }

        /// <summary>
        /// 1:1 port of Riot's BB <c>BBExecutePeriodically</c> (BuildingBlocksBase.lua:1946). Fires
        /// <paramref name="action"/> on a fixed cadence, driven by the every-tick script update
        /// (IBuffGameScript.OnUpdate / AreaTrigger onUpdate). Call it once per update. The schedule
        /// anchor is stored in <paramref name="trackTable"/> under <paramref name="trackVar"/> — pass
        /// the instance's own VariableTable (Riot TrackTimeVarTable): a buff's <c>BuffVars</c>, a
        /// cast's <c>InstanceVars</c>, etc.
        ///
        /// Faithful semantics (verified against the decompiled Lua):
        /// <list type="bullet">
        /// <item>Absolute game clock (<see cref="ApiMapFunctionManager.GameTime"/>, ms), NOT a delta
        /// accumulator.</item>
        /// <item>First call anchors the schedule to <c>now</c>; fires immediately only if
        /// <paramref name="executeImmediately"/> is true, else the first tick is one full interval
        /// later.</item>
        /// <item>Advances the anchor by <c>+= intervalMs</c> (carries the overshoot — zero drift),
        /// never <c>= now</c>.</item>
        /// <item>One fire per call (Riot uses <c>if</c>, not <c>while</c>): on a lag spike it catches
        /// up at most one tick per update, never bursts all missed ticks at once.</item>
        /// <item>Effective interval == 0 → fires every call (<c>track + 0 &lt;= now</c> is
        /// always true).</item>
        /// <item>Interval resolution mirrors the Lua: <paramref name="intervalMs"/> is the base
        /// (Riot <c>GetParam("TimeBetweenExecutions")</c>); if <paramref name="tickTimeVar"/> is set
        /// and its key holds a value in <paramref name="tickTimeTable"/>, that value OVERRIDES the base
        /// (Riot TickTimeVar). The override is re-read every call, so the cadence can change at runtime.</item>
        /// <item><paramref name="maxTicks"/> caps the TOTAL number of fires (the executeImmediately fire
        /// counts); pass 0 for unlimited. Riot's BBExecutePeriodically has NO cap — scripts count
        /// caller-side (e.g. Pantheon E's <c>ticksRemaining</c>), so this is a LeagueSandbox convenience;
        /// the count lives beside the anchor, sharing the table's lifecycle (auto-reset per cast for
        /// InstanceVars / per buff for BuffVars).</item>
        /// </list>
        /// </summary>
        /// <param name="trackTable">Instance VariableTable holding the schedule anchor (Riot TrackTimeVarTable).</param>
        /// <param name="trackVar">Key under which the anchor timestamp is stored (Riot TrackTimeVar).</param>
        /// <param name="intervalMs">Base cadence in ms (Riot GetParam TimeBetweenExecutions; GameTime is ms).</param>
        /// <param name="executeImmediately">Fire once on the first call (Riot ExecuteImmediately).</param>
        /// <param name="maxTicks">Cap on total fires (0 = unlimited). LeagueSandbox extension ≙ Riot's caller-side counter.</param>
        /// <param name="action">The effect to run per tick (Riot SubBlocks).</param>
        /// <param name="tickTimeTable">Optional table holding a runtime interval override (Riot TickTimeVarTable).</param>
        /// <param name="tickTimeVar">Optional key of the runtime interval override (Riot TickTimeVar); null = no override.</param>
        public static void ExecutePeriodically(
            VariableTable trackTable, string trackVar, float intervalMs, bool executeImmediately, int maxTicks, Action action,
            VariableTable tickTimeTable = null, string tickTimeVar = null)
        {
            float now = ApiMapFunctionManager.GameTime();

            // Riot: interval = GetParam("TimeBetweenExecutions"), then TickTimeVar overrides it when set.
            // Read every call so a script can retune the cadence on the fly; falls back to the base otherwise.
            float interval = intervalMs;
            if (tickTimeVar != null && tickTimeTable != null
                && tickTimeTable.TryGet<float>(tickTimeVar, out var tickOverride))
            {
                interval = tickOverride;
            }

            // maxTicks cap (LeagueSandbox extension — not in Riot's BB; Riot caps caller-side via a
            // separate counter like Pantheon E's ticksRemaining). The fire count is stored next to the
            // anchor, so it auto-resets with trackTable's lifecycle (fresh per cast/per buff).
            string countKey = trackVar + "$n";
            int fired = trackTable.GetInt(countKey);
            if (maxTicks > 0 && fired >= maxTicks)
            {
                return;
            }

            void Fire()
            {
                action();
                fired++;
                trackTable.Set(countKey, fired);
            }

            if (!trackTable.TryGet<float>(trackVar, out var track))
            {
                track = now;
                trackTable.Set(trackVar, track);
                if (executeImmediately)
                {
                    Fire();
                }
            }
            if ((maxTicks == 0 || fired < maxTicks) && track + interval <= now)
            {
                trackTable.Set(trackVar, track + interval);
                Fire();
            }
        }

        /// <summary>
        /// 1:1 port of Riot's BB <c>BBExecutePeriodicallyReset</c> (BuildingBlocksBase.lua:1978): clears
        /// the schedule anchor (Lua <c>track[TrackTimeVar] = nil</c>). After a reset the next
        /// <see cref="ExecutePeriodically"/> call re-anchors to <c>now</c> and, if ExecuteImmediately is
        /// set there, fires once again. Pass the SAME table + key used for the paired ExecutePeriodically.
        /// </summary>
        /// <param name="trackTable">The table the paired ExecutePeriodically anchors into (Riot TrackTimeVarTable).</param>
        /// <param name="trackVar">The anchor key to clear (Riot TrackTimeVar).</param>
        public static void ExecutePeriodicallyReset(VariableTable trackTable, string trackVar)
        {
            trackTable.Remove(trackVar);
        }

        /// <summary>
        /// Returns a random point inside the ring centered on <paramref name="center"/> bounded by
        /// <paramref name="innerRadius"/> and <paramref name="radius"/>. Mirrors Riot's
        /// GetRandomAreaPoint (LuaBuildingBlockHelper.cpp): the radius is drawn LINEARLY in
        /// [innerRadius, radius] (NOT area-uniform — no sqrt, so points cluster toward the center)
        /// and the angle uniformly in [0, 2π). When innerRadius == radius the result lies exactly on
        /// a circle of that radius. The engine works on the X/Z ground plane, which maps to our
        /// Vector2 X/Y.
        /// </summary>
        /// <param name="center">Center of the area.</param>
        /// <param name="radius">Outer radius.</param>
        /// <param name="innerRadius">Inner radius (0 = full disc). Must be &lt;= radius.</param>
        /// <returns>A random Vector2 point within the ring.</returns>
        public static Vector2 GetRandomPointInArea(Vector2 center, float radius, float innerRadius = 0f)
        {
            // Riot hard-asserts innerRadius <= radius; clamp defensively instead of crashing.
            if (innerRadius > radius)
            {
                innerRadius = radius;
            }

            float r = innerRadius + (radius - innerRadius) * (float)_random.NextDouble();
            float angle = (float)(_random.NextDouble() * Math.PI * 2.0);
            return center + new Vector2(r * (float)Math.Cos(angle), r * (float)Math.Sin(angle));
        }

        /// <summary>
        /// BBGetRandomPointInAreaUnit: a random point in the ring around <paramref name="target"/>'s
        /// position. See <see cref="GetRandomPointInArea"/> for the distribution.
        /// </summary>
        /// <param name="target">Unit the area is centered on.</param>
        /// <param name="radius">Outer radius.</param>
        /// <param name="innerRadius">Inner radius (0 = full disc). Must be &lt;= radius.</param>
        /// <returns>A random Vector2 point within the ring around the unit.</returns>
        public static Vector2 GetRandomPointInAreaUnit(GameObject target, float radius, float innerRadius = 0f)
        {
            return GetRandomPointInArea(target.Position, radius, innerRadius);
        }

        /// <summary>
        /// Rolls a critical strike for a spell EXACTLY like an auto-attack: uses <paramref name="caster"/>'s
        /// CriticalChance via the same crit-karma roll as AAs (so it only crits by chance unless crit chance is
        /// 100%). Returns true on crit. The caller then applies the crit damage — multiply the damage by
        /// <c>caster.Stats.CriticalDamage.Total</c> BEFORE dealing it, and pass
        /// <c>DamageResultType.RESULT_CRITICAL</c> to TakeDamage for the crit splash text (that flag is
        /// visual-only; it does not multiply). Used e.g. by Master Yi's Alpha Strike.
        /// </summary>
        /// <param name="caster">The unit whose CriticalChance is rolled.</param>
        /// <param name="target">The unit being hit (crit-karma stream depends on its class).</param>
        /// <returns>True if this hit crits.</returns>
        public static bool RollCrit(ObjAIBase caster, AttackableUnit target)
        {
            if (caster == null || target == null)
            {
                return false;
            }
            return caster.RollCrit(target);
        }

        /// <summary>
        /// Reports whether or not the specified coordinates are walkable.
        /// </summary>
        /// <param name="x">X coordinaate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="checkRadius">Radius around the given point to check for walkability.</param>
        /// <returns>True/False</returns>
        public static bool IsWalkable(float x, float y, float checkRadius = 0)
        {
            return _game.Map.PathingHandler.IsWalkable(new Vector2(x, y), checkRadius);
        }

        /// <summary>
        /// Adds a named buff with the given duration, stacks, and origin spell to a unit.
        /// From = owner of the spell (usually caster).
        /// </summary>
        /// <param name="buffName">Internally named buff to add.</param>
        /// <param name="duration">Time in seconds the buff should last.</param>
        /// <param name="stacks">Stacks of the buff to add.</param>
        /// <param name="originspell">Spell which called this function.</param>
        /// <param name="onto">Target of the buff.</param>
        /// <param name="from">Owner of the buff.</param>
        /// <param name="infiniteduration">Whether or not the buff should last forever.</param>
        /// <param name="buffAddType">BB BuffAddType override (RENEW/REPLACE/STACKS_*). Null → the buff
        /// script's BuffMetaData default. Mirrors BBSpellBuffAdd's per-call BuffAddType.</param>
        /// <param name="buffType">BB BuffType override (INTERNAL/DAMAGE/STUN/…). Null → script default.</param>
        /// <param name="maxStacks">BB MaxStack override. Null → script default.</param>
        /// <param name="canMitigateDuration">BB CanMitigateDuration: when false, tenacity does NOT
        /// shorten the duration even for a tenacity-reducible BuffType (per-buff opt-out). Default true.</param>
        /// <param name="isHidden">BB IsHiddenOnClient override. Null → script default.</param>
        /// <returns>New buff instance.</returns>
        // NOTE (BB parity gaps, intentional): BBSpellBuffAdd's `StacksExclusive` has no engine concept
        // (moot for the MaxStack=1 majority) and `TickRate` is not a buff-level knob here — periodic
        // buff effects use ExecutePeriodically (BBExecutePeriodically), see that helper.
        public static Buff AddBuff(
            string buffName,
            float duration,
            byte stacks,
            Spell originspell,
            AttackableUnit onto,
            ObjAIBase from,
            bool infiniteduration = false,
            IEventSource parent = null,
            VariableTable variableTable = null,
            BuffAddType? buffAddType = null,
            BuffType? buffType = null,
            int? maxStacks = null,
            bool canMitigateDuration = true,
            bool? isHidden = null
        )
        {
            Buff buff;

            try
            {
                buff = new Buff(_game, buffName, duration, stacks, originspell, onto, from, infiniteduration, parent,
                    variableTable, skipTenacity: !canMitigateDuration,
                    buffAddType: buffAddType, buffType: buffType, maxStacks: maxStacks, isHidden: isHidden);
            }
            catch (ArgumentException exception)
            {
                _logger.Error(exception);
                return null;
            }

            onto.AddBuff(buff);
            return buff;
        }

        /// <summary>
        /// Whether or not the specified unit has the specified buff instance.
        /// </summary>
        /// <param name="unit">Unit to check.</param>
        /// <param name="b">Buff to check for.</param>
        /// <returns>True/False</returns>
        public static bool HasBuff(AttackableUnit unit, Buff b)
        {
            return unit.HasBuff(b);
        }

        /// <summary>
        /// Whether or not the specified unit has a buff with the given name.
        /// </summary>
        /// <param name="unit">Unit to check.</param>
        /// <param name="b">Buff name to check for.</param>
        /// <returns>True/False</returns>
        public static bool HasBuff(AttackableUnit unit, string b)
        {
            return unit.HasBuff(b);
        }

        /// <summary>
        /// Whether the specified unit has any buff of the given BuffType.
        /// Mirrors Lua API <c>HasBuffOfType</c>.
        /// </summary>
        public static bool HasBuffOfType(AttackableUnit unit, BuffType type)
        {
            return unit.HasBuffType(type);
        }

        /// <summary>
        /// Engine-clock time (milliseconds, same scale as <c>GameTime()</c>) at which the unit last took
        /// real damage, or 0 if never. Mirrors Lua API <c>GetLastTookDamageTime</c>
        /// (Riot GameObject::mLastTookDamageTime). Use the <c>GameTime() - GetLastTookDamageTime(unit)</c>
        /// pattern for "time since last in combat" (out-of-combat regen, TaskDefendStructure).
        /// </summary>
        public static float GetLastTookDamageTime(AttackableUnit unit)
        {
            return unit.LastTookDamageTime;
        }

        /// <summary>
        /// Returns the parent buff's stack count for the given name, or 0 if no such buff is active.
        /// Mirrors Lua API <c>SpellBuffCount</c> (2-arg, source-agnostic).
        /// </summary>
        public static int GetBuffStackCount(AttackableUnit unit, string buffName)
        {
            return unit.GetBuffWithName(buffName)?.StackCount ?? 0;
        }

        /// <summary>
        /// Returns the total stacks of the named buff that originate from <paramref name="source"/>, or 0.
        /// Mirrors Lua API <c>SpellBuffCount(unit, buffName, sourceUnit)</c> (3-arg, caster-filtered) —
        /// e.g. BuildingBlocksBase.lua:1048 <c>SpellBuffCount(unit, Name, Attacker) &gt; 0</c>. The 2-arg
        /// overload aggregates across all casters; this one only counts instances whose
        /// <see cref="Buff.SourceUnit"/> matches, which matters for STACKS_AND_OVERLAPS buffs applied by
        /// multiple casters (each application is a separate instance with its own source).
        /// </summary>
        public static int GetBuffStackCount(AttackableUnit unit, string buffName, ObjAIBase source)
        {
            int total = 0;
            foreach (var b in unit.GetBuffsWithName(buffName))
            {
                if (b.SourceUnit == source)
                {
                    total += b.StackCount;
                }
            }
            return total;
        }

        /// <summary>
        /// Adds a buff only if no buff with the same name is currently active on the target — does not refresh
        /// or re-stack an existing one. Mirrors Lua API <c>SpellBuffAddNoRenew</c>.
        /// </summary>
        /// <returns>The new buff if it was added, otherwise null.</returns>
        public static Buff AddBuffNoRenew(
            string buffName,
            float duration,
            byte stacks,
            Spell originSpell,
            AttackableUnit onto,
            ObjAIBase from,
            bool infiniteDuration = false,
            IEventSource parent = null,
            VariableTable variableTable = null
        )
        {
            if (onto.HasBuff(buffName))
            {
                return null;
            }
            return AddBuff(buffName, duration, stacks, originSpell, onto, from, infiniteDuration, parent, variableTable);
        }

        /// <summary>
        /// Sets the stacks of the specified buff instance to the given number of stacks.
        /// </summary>
        /// <param name="b">Buff instance.</param>
        /// <param name="newStacks">Stacks to set.</param>
        public static void EditBuff(Buff b, byte newStacks)
        {
            if (b == null)
            {
                return;
            }

            // Avoid default stack packet routing so counter buffs can use their dedicated packet type.
            b.SetStacks(newStacks, false);
            if (b.Hidden || _game?.PacketNotifier == null)
            {
                return;
            }

            if (b.BuffType == BuffType.COUNTER)
            {
                _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(b);
            }
            else
            {
                _game.PacketNotifier.NotifyNPC_BuffUpdateCount(b, b.Duration, b.TimeElapsed);
            }
        }

        /// <summary>
        /// Sets the counter of a COUNTER-type buff to the given value. Unlike the byte overload,
        /// the value travels as an int32 via NPC_BuffUpdateNumCounter (client SetCounter(int)), so
        /// values above 255 are preserved — e.g. Sion W's Soul Furnace shield amount, which is
        /// replay-verified to reach 500+ (NPC_BuffUpdateNumCounter, BuffType=COUNTER).
        /// </summary>
        /// <param name="b">Buff instance.</param>
        /// <param name="newCounter">Counter value to set.</param>
        public static void EditBuffCounter(Buff b, int newCounter)
        {
            if (b == null)
            {
                return;
            }

            b.SetStacks(newCounter, false);
            if (b.Hidden || _game?.PacketNotifier == null)
            {
                return;
            }

            _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(b);
        }

        /// <summary>
        /// Updates only the client-side timer visuals for a buff icon.
        /// Useful for infinite buffs that need a custom recharge indicator.
        /// </summary>
        /// <param name="b">Buff instance.</param>
        /// <param name="durationSeconds">Displayed total duration in seconds.</param>
        /// <param name="runningTimeSeconds">Displayed elapsed/running time in seconds.</param>
        public static void SetBuffClientTimer(Buff b, float durationSeconds, float runningTimeSeconds = 0f)
        {
            if (b == null || b.Hidden)
            {
                return;
            }

            if (durationSeconds < 0f)
            {
                durationSeconds = 0f;
            }

            if (runningTimeSeconds < 0f)
            {
                runningTimeSeconds = 0f;
            }

            _game.PacketNotifier.NotifyNPC_BuffReplace(b, durationSeconds, runningTimeSeconds);
        }

        /// <summary>
        /// Removes the specified buff from any AI units it is applied to and runs OnDeactivate callback for the buff's script.
        /// If the buff's BuffAddType is STACKS_AND_OVERLAPS, each stack is individually instanced, so only one stack is removed.
        /// </summary>
        /// <param name="buff">Buff instance to remove.</param>
        public static void RemoveBuff(Buff buff)
        {
            buff.DeactivateBuff();
        }

        /// <summary>
        /// Removes all buffs of the given name from the specified AI unit and runs OnDeactivate callback for the buff's script.
        /// Even if the buff's BuffAddType is STACKS_AND_OVERLAPS, it will still remove all buff instances.
        /// </summary>
        /// <param name="target">AI unit to check.</param>
        /// <param name="buff">Buff name to remove.</param>
        public static void RemoveBuff(AttackableUnit target, string buff)
        {
            target.RemoveBuffsWithName(buff);
        }

        /// <summary>
        /// Removes only the named buff instances on <paramref name="target"/> that originate from
        /// <paramref name="source"/>, running each one's OnDeactivate. Mirrors Lua API
        /// <c>SpellBuffRemove(unit, buffName, sourceUnit)</c> (3-arg, caster-filtered) —
        /// e.g. BuildingBlocksBase.lua:2379 <c>SpellBuffRemove(unit, Name, Attacker)</c>. Unlike the 2-arg
        /// overload (which strips ALL same-named instances regardless of caster), this leaves other casters'
        /// instances intact — required for STACKS_AND_OVERLAPS debuffs shared by multiple casters.
        /// </summary>
        /// <param name="target">AI unit to remove buffs from.</param>
        /// <param name="buff">Buff name to remove.</param>
        /// <param name="source">Only instances whose <see cref="Buff.SourceUnit"/> equals this are removed.</param>
        public static void RemoveBuff(AttackableUnit target, string buff, ObjAIBase source)
        {
            // Iterate a snapshot: DeactivateBuff mutates the underlying buff collections.
            foreach (var b in target.GetBuffsWithName(buff).ToArray())
            {
                if (b.SourceUnit == source)
                {
                    b.DeactivateBuff();
                }
            }
        }

        /// <summary>
        /// Removes every buff on the target whose BuffType matches. Mirrors Lua API <c>SpellBuffRemoveType</c>
        /// (e.g. <c>SpellBuffRemoveType(me, BUFF_Taunt)</c> in Hero.lua to break taunt-charm).
        /// </summary>
        public static void RemoveBuffsOfType(AttackableUnit target, BuffType type)
        {
            target.RemoveBuffsByType(type);
        }

        /// <summary>
        /// Whether the given buff is a spell-shield break attempt. Riot uses two coexisting patterns
        /// (see project_spell_shield_system memory): the generic "SpellShieldMarker" applied via
        /// BB <c>BBBreakSpellShields</c> (BuildingBlocksBase.lua:3471), and per-spell
        /// "&lt;Champ&gt;&lt;Spell&gt;SpellShieldCheck" window buffs (KatarinaQMark/VarusQ/E/R/YasuoQ/
        /// LissandraR, all present as 4.20 luaobjs) for spells with conditional follow-up effects.
        /// Spell-shield buff scripts match on this from their OnUnitBuffActivated hook.
        /// </summary>
        public static bool IsSpellShieldBreaker(Buff buff)
        {
            return buff.Name == "SpellShieldMarker" || buff.Name.EndsWith("SpellShieldCheck");
        }

        /// <summary>
        /// Script-facing equivalent of BB <c>BBBreakSpellShields</c> (BuildingBlocksBase.lua:3471) for
        /// break attempts OUTSIDE the automatic <c>Spell.ApplyEffects</c> gate (which every spell
        /// execution already passes through — scripts normally do NOT need to call this).
        /// Routes through <see cref="AttackableUnit.ConsumeSpellShield"/>: wire-silent, ally-safe,
        /// notifies the shield script via OnSpellShieldBroken. Replay-verified: Riot's break sends NO
        /// SpellShieldMarker packet — the marker buff name survives only as BB-script legacy
        /// (nothing in the 4.20 corpus calls the BB function; see project_spell_shield_system memory).
        /// </summary>
        /// <returns>
        /// True if the effect may proceed (no spell shield blocked it), false if a shield consumed the hit.
        /// Spells whose script metadata sets <c>DoesntBreakShields</c> (SpellMetaData.txt: "the spell's
        /// execution won't break shields", e.g. Absolute Zero ticks) never attempt the break and always proceed.
        /// </returns>
        public static bool BreakSpellShields(AttackableUnit target, Spell originSpell)
        {
            if (originSpell?.Script?.ScriptMetadata?.DoesntBreakShields == true)
            {
                return true;
            }

            return !target.ConsumeSpellShield(originSpell);
        }


        /// <summary>
        ///     Whether the specified unit is within its team's fountain area.
        /// </summary>
        /// <param name="unit">Unit to check.</param>
        /// <param name="range">Fountain radius to check against. Defaults to 1000.</param>
        /// <returns>True if within fountain range.</returns>
        public static bool IsInFountain(AttackableUnit unit, float range = 1000.0f) {
            var fountainPos = _game.Map.MapScript.GetFountainPosition(unit.Team);
            return Vector2.DistanceSquared(unit.Position, fountainPos) <= range * range;
        }

        /// <summary>
        /// Adds a shield to an AttackableUnit with specified values.
        /// </summary>
        /// <param name="source">Source unit of the shield.</param>
        /// <param name="target">Target unit to shield.</param>
        /// <param name="amount">Shield amount.</param>
        /// <param name="physical">Whether the shield blocks physical damage.</param>
        /// <param name="magical">Whether the shield blocks magical damage.</param>
        /// <param name="owner">Optional owning buff. Pass it so the shield inherits the buff's
        /// OnPreDamagePriority / DoOnPreDamageInExpirationOrder and remaining lifetime, which drive
        /// the consumption order when a unit holds several shields at once.</param>
        /// <returns>New shield instance.</returns>
        public static Shield AddShield(
            ObjAIBase source,
            AttackableUnit target,
            float amount,
            bool physical = true,
            bool magical = true,
            Buff owner = null
        )
        {
            var shield = new Shield(source, target, physical, magical, amount, owner);
            target.AddShield(shield);
            return shield;
        }

        /// <summary>
        /// Removes a shield from an AttackableUnit immediately.
        /// </summary>
        /// <param name="target">Target unit to remove the shield from.</param>
        /// <param name="shield">Shield to remove.</param>
        public static void RemoveShield(AttackableUnit target, Shield shield)
        {
            target.RemoveShield(shield);
        }

        /// <summary>
        /// Checks whether an AttackableUnit has any shield or a specific shield.
        /// </summary>
        /// <param name="target">Target unit to check.</param>
        /// <param name="shield">Optional shield instance to look for.</param>
        /// <returns>True if a shield is present.</returns>
        public static bool HasShield(AttackableUnit target, Shield shield = null)
        {
            return target.HasShield(shield);
        }

        /// <summary>
        /// Reduces a shield's amount by the given (positive) value and notifies the client so the
        /// shield bar shrinks. If the shield drains to 0 it is removed via its holder — which fires
        /// OnShieldBreak, the same break path as damage consumption (ConsumeShields).
        /// </summary>
        public static void ReduceShield(Shield shield, float amount)
        {
            // Resolve the holder FIRST: reducing a shield that isn't on a unit would mutate its
            // amount with no way to notify the client or remove it (orphan shield + bar desync).
            var unit = shield?.TargetUnit;
            if (unit == null || amount <= 0)
            {
                return;
            }

            float applied = shield.Reduce(amount);
            if (applied <= 0)
            {
                return;
            }

            _game.PacketNotifier.NotifyModifyShield(unit, -applied, shield.Physical, shield.Magical, false);
            ApiEventManager.OnShieldReduced.Publish(shield, applied);
            if (shield.IsConsumed())
            {
                unit.RemoveShield(shield);
            }
        }

        /// <summary>
        /// Increases a shield's amount by the given (positive) value and notifies the client so
        /// the shield bar grows.
        /// </summary>
        public static void IncShield(Shield shield, float amount)
        {
            var unit = shield?.TargetUnit;
            if (unit == null || amount <= 0)
            {
                return;
            }

            float applied = shield.Increase(amount);
            if (applied <= 0)
            {
                return;
            }

            _game.PacketNotifier.NotifyModifyShield(unit, applied, shield.Physical, shield.Magical, false);
            ApiEventManager.OnShieldIncreased.Publish(shield, applied);
        }

        /// <summary>
        /// Gets the target's current primary ability resource.
        /// </summary>
        /// <param name="target">Unit to query.</param>
        /// <returns>Current PAR value, or 0 if target is null.</returns>
        public static float GetPAR(AttackableUnit target)
        {
            return target?.GetPAR() ?? 0.0f;
        }

        /// <summary>
        /// Gets the target's current primary ability resource if the PAR type is compatible.
        /// </summary>
        /// <param name="target">Unit to query.</param>
        /// <param name="parType">Required PAR type.</param>
        /// <returns>Current PAR value, or 0 if target is null or type-incompatible.</returns>
        public static float GetPAR(AttackableUnit target, PrimaryAbilityResourceType parType)
        {
            return target != null && target.HasCompatiblePARType(parType) ? target.GetPAR() : 0.0f;
        }

        /// <summary>
        /// Sends S2C_UnitSetPARType (0x113), switching <paramref name="unit"/>'s resource-bar TYPE
        /// (the bar style — Mana/Energy/Heat/…) on clients. The unit is identified by the packet's
        /// header SenderNetID (extended-packet routing) — the body carries only the type byte.
        /// NOTE: this only changes the CLIENT bar type; the server-side Stats.ParType is separate.
        /// Riot never sends this in normal 4.20 play (PAR type is static, set at spawn from champion
        /// data) — provided for custom/runtime PAR-type changes.
        /// </summary>
        /// <param name="unit">Unit whose PAR (resource-bar) type to set.</param>
        /// <param name="parType">The PAR type to switch the bar to.</param>
        public static void SetUnitPARType(AttackableUnit unit, PrimaryAbilityResourceType parType)
        {
            if (unit == null)
            {
                return;
            }
            _game.PacketNotifier.NotifyUnitSetPARType(unit, parType);
        }

        /// <summary>
        /// Gets the target's maximum primary ability resource.
        /// </summary>
        /// <param name="target">Unit to query.</param>
        /// <returns>Maximum PAR value, or 0 if target is null.</returns>
        public static float GetMaxPAR(AttackableUnit target)
        {
            return target?.GetMaxPAR() ?? 0.0f;
        }

        /// <summary>
        /// Gets the target's maximum primary ability resource if the PAR type is compatible.
        /// </summary>
        /// <param name="target">Unit to query.</param>
        /// <param name="parType">Required PAR type.</param>
        /// <returns>Maximum PAR value, or 0 if target is null or type-incompatible.</returns>
        public static float GetMaxPAR(AttackableUnit target, PrimaryAbilityResourceType parType)
        {
            return target != null && target.HasCompatiblePARType(parType) ? target.GetMaxPAR() : 0.0f;
        }

        /// <summary>
        /// Gets the target's current PAR ratio.
        /// </summary>
        /// <param name="target">Unit to query.</param>
        /// <returns>PAR ratio, or 0 if target is null.</returns>
        public static float GetPARPercent(AttackableUnit target)
        {
            return target?.GetPARPercent() ?? 0.0f;
        }

        /// <summary>
        /// Gets the target's current PAR ratio if the PAR type is compatible.
        /// </summary>
        /// <param name="target">Unit to query.</param>
        /// <param name="parType">Required PAR type.</param>
        /// <returns>PAR ratio, or 0 if target is null or type-incompatible.</returns>
        public static float GetPARPercent(AttackableUnit target, PrimaryAbilityResourceType parType)
        {
            return target != null && target.HasCompatiblePARType(parType) ? target.GetPARPercent() : 0.0f;
        }

        /// <summary>
        /// Sets the target's PAR visual state.
        /// </summary>
        /// <param name="target">Unit whose PAR state should be updated.</param>
        /// <param name="parState">State value to apply (typically 0/1).</param>
        /// <param name="userId">Optional user id. If not set, packet is sent using vision broadcast rules.</param>
        public static void SetPARState(AttackableUnit target, uint parState, int userId = -1)
        {
            if (target == null)
            {
                return;
            }

            _game.PacketNotifier.NotifySetParState(target, parState, userId);
        }

        /// <summary>
        /// Shows or hides the specified unit's health bar on the client.
        /// </summary>
        /// <param name="target">Unit whose health bar visibility should change.</param>
        /// <param name="userId">UserId to send to. If -1, broadcasts to all players.</param>
        /// <param name="hide">True to hide the health bar, false to show it.</param>
        public static void HideHealthBar(AttackableUnit target, int userId = -1, bool hide = true)
        {
            if (target == null)
            {
                return;
            }

            _game.PacketNotifier.NotifyShowHealthBar(target, userId, hide);
        }

        /// <summary>
        /// Checks whether the target uses the specified PAR type.
        /// </summary>
        /// <param name="target">Unit to query.</param>
        /// <param name="parType">PAR type to compare against.</param>
        /// <returns>True if target exists and matches the PAR type; otherwise false.</returns>
        public static bool HasPARType(AttackableUnit target, PrimaryAbilityResourceType parType)
        {
            return target != null && target.HasPARType(parType);
        }

        /// <summary>
        /// Checks whether the target is compatible with the specified PAR type.
        /// </summary>
        /// <param name="target">Unit to query.</param>
        /// <param name="parType">PAR type requirement.</param>
        /// <returns>True if target exists and PAR type is compatible; otherwise false.</returns>
        public static bool HasCompatiblePARType(AttackableUnit target, PrimaryAbilityResourceType parType)
        {
            return target != null && target.HasCompatiblePARType(parType);
        }

        /// <summary>
        /// Checks whether the target has enough PAR for the requested amount.
        /// </summary>
        /// <param name="target">Unit to query.</param>
        /// <param name="amount">Required PAR amount.</param>
        /// <returns>True if target exists and has enough PAR; otherwise false.</returns>
        public static bool HasEnoughPAR(AttackableUnit target, float amount)
        {
            return target != null && target.HasEnoughPAR(amount);
        }

        /// <summary>
        /// Increases the target's PAR by the given amount.
        /// </summary>
        /// <param name="target">Unit whose PAR will be increased.</param>
        /// <param name="amount">Requested PAR amount to add.</param>
        /// <param name="source">Unit credited as the source of the PAR gain.</param>
        /// <returns>Actual PAR amount added, or 0 if target is null.</returns>
        public static float IncreasePAR(AttackableUnit target, float amount, AttackableUnit source = null)
        {
            if (target == null)
            {
                return 0.0f;
            }

            return target.IncreasePAR(source ?? target, amount);
        }

        /// <summary>
        /// Increases the target's PAR by the given amount when the requested PAR type is compatible.
        /// </summary>
        /// <param name="target">Unit whose PAR will be increased.</param>
        /// <param name="amount">Requested PAR amount to add.</param>
        /// <param name="parType">Required PAR type.</param>
        /// <param name="source">Unit credited as the source of the PAR gain.</param>
        /// <returns>Actual PAR amount added, or 0 if target is null or type-incompatible.</returns>
        public static float IncreasePAR(
            AttackableUnit target,
            float amount,
            PrimaryAbilityResourceType parType,
            AttackableUnit source = null
        )
        {
            if (target == null || !target.HasCompatiblePARType(parType))
            {
                return 0.0f;
            }

            return target.IncreasePAR(source ?? target, amount);
        }

        /// <summary>
        /// Spends PAR from the target.
        /// </summary>
        /// <param name="target">Unit whose PAR will be spent.</param>
        /// <param name="amount">Requested PAR amount to spend.</param>
        /// <returns>Actual PAR amount spent, or 0 if target is null.</returns>
        public static float SpendPAR(AttackableUnit target, float amount)
        {
            if (target == null)
            {
                return 0.0f;
            }

            return target.SpendPAR(amount);
        }

        /// <summary>
        /// Creates a new particle with the specified parameters.
        /// </summary>
        /// <param name="caster">GameObject that caused this particle to spawn.</param>
        /// <param name="particle">Internal name of the particle.</param>
        /// <param name="start">Position to spawn at.</param>
        /// <param name="end">Position to end at.</param>
        /// <param name="lifetime">Time in seconds the particle should last.</param>
        /// <param name="size">Scale.</param>
        /// <param name="bone">Bone on the owner the particle should be attached to.</param>
        /// <param name="targetBone">Bone on the target the particle should be attached to.</param>
        /// <param name="direction">3D direction the particle should face.</param>
        /// <param name="followGroundTilt">Whether or not the particle should be titled along the ground towards its end position.</param>
        /// <param name="reqVision">Whether or not the particle can be obstructed by terrain.</param>
        /// <param name="teamOnly">The only team which should be able to see the particle.</param>
        /// <param name="flags">Flags which determine how the particle behaves. Refer to FXFlags enum.</param>
        /// <returns>New particle instance.</returns>
        public static Particle AddParticlePos(GameObject caster, string particle, Vector2 start, Vector2 end,
            float lifetime = 1.0f, float size = 1.0f, string bone = "", string targetBone = "",
            Vector3 direction = new Vector3(), bool followGroundTilt = false, TeamId teamOnly = TeamId.TEAM_ALL,
            GameObject unitOnly = null, FXFlags flags = FXFlags.SimulateWhileOffScreen, bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f, string enemyParticle = null, GameObject keywordObject = null,
            float fowVisionRadius = 0f, bool affectedByFoW = true, bool sendIfOnScreenOrDiscard = false)
        {
            var p = new Particle(_game, caster, start, end, particle, size, bone, targetBone, 0, direction,
                followGroundTilt, lifetime, teamOnly, unitOnly, flags, ignoreCasterVisibility, overrideTargetHeight,
                enemyParticle, keywordObject?.NetId, fowVisionRadius, affectedByFoW, sendIfOnScreenOrDiscard);
            return p;
        }

        /// <summary>
        /// Creates a new particle with the specified parameters.
        /// </summary>
        /// <param name="caster">GameObject that caused this particle to spawn.</param>
        /// <param name="bindObj">GameObject that the particle should bind to.</param>
        /// <param name="particle">Internal name of the particle.</param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="lifetime">Time in seconds the particle should last.</param>
        /// <param name="size">Scale.</param>
        /// <param name="bone">Bone on the owner the particle should be attached to.</param>
        /// <param name="targetBone">Bone on the target the particle should be attached to.</param>
        /// <param name="direction">3D direction the particle should face.</param>
        /// <param name="followGroundTilt">Whether or not the particle should be titled along the ground towards its end position.</param>
        /// <param name="reqVision">Whether or not the particle can be obstructed by terrain.</param>
        /// <param name="teamOnly">The only team which should be able to see the particle.</param>
        /// <param name="flags">Flags which determine how the particle behaves. Refer to FXFlags enum.</param>
        /// <returns>New particle instance.</returns>
        public static Particle AddParticle(GameObject caster, GameObject bindObj, string particle, Vector2 position,
            float lifetime = 1.0f, float size = 1.0f, string bone = "", string targetBone = "",
            Vector3 direction = new Vector3(), bool followGroundTilt = false, TeamId teamOnly = TeamId.TEAM_ALL,
            GameObject unitOnly = null, FXFlags flags = FXFlags.SimulateWhileOffScreen, bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f, string enemyParticle = null,
            GameObject keywordObject = null, float fowVisionRadius = 0f, bool affectedByFoW = true,
            bool sendIfOnScreenOrDiscard = false, uint? packageHashOverride = null, bool sendUnbatched = false)
        {
            return new Particle(_game, caster, bindObj, position, particle, size, bone, targetBone, 0, direction,
                followGroundTilt, lifetime, teamOnly, unitOnly, flags, ignoreCasterVisibility, overrideTargetHeight,
                enemyParticle, keywordObject?.NetId, fowVisionRadius, affectedByFoW, sendIfOnScreenOrDiscard,
                packageHashOverride, sendUnbatched);
        }

        /// <summary>
        /// Creates a new particle with the specified parameters.
        /// This particle will be attached to a target.
        /// </summary>
        /// <param name="caster">GameObject that caused this particle to spawn.</param>
        /// <param name="bindObj">GameObject that the particle should bind to (prioritized over target).</param>
        /// <param name="particle">Internal name of the particle.</param>
        /// <param name="target">GameObject that the particle should bind to after the bindObj.</param>
        /// <param name="lifetime">Time in seconds the particle should last.</param>
        /// <param name="size">Scale.</param>
        /// <param name="bone">Bone on the owner the particle should be attached to.</param>
        /// <param name="targetBone">Bone on the target the particle should be attached to.</param>
        /// <param name="direction">3D direction the particle should face.</param>
        /// <param name="followGroundTilt">Whether or not the particle should be titled along the ground towards its end position.</param>
        /// <param name="reqVision">Whether or not the particle can be obstructed by terrain.</param>
        /// <param name="teamOnly">The only team which should be able to see the particle.</param>
        /// <param name="flags">Flags which determine how the particle behaves. Refer to FXFlags enum.</param>
        /// <returns>New particle instance.</returns>
        public static Particle AddParticleTarget(GameObject caster, GameObject bindObj, string particle,
            GameObject target, float lifetime = 1.0f, float size = 1.0f, string bone = "", string targetBone = "",
            Vector3 direction = new Vector3(), bool followGroundTilt = false, TeamId teamOnly = TeamId.TEAM_ALL,
            GameObject unitOnly = null, FXFlags flags = FXFlags.SimulateWhileOffScreen, bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f, string enemyParticle = null, GameObject keywordObject = null,
            float fowVisionRadius = 0f, bool affectedByFoW = true, bool sendIfOnScreenOrDiscard = false,
            uint? packageHashOverride = null, bool sendUnbatched = false)
        {
            var p = new Particle(_game, caster, bindObj, target, particle, size, bone, targetBone, 0, direction,
                followGroundTilt, lifetime, teamOnly, unitOnly, flags, ignoreCasterVisibility, overrideTargetHeight,
                enemyParticle, keywordObject?.NetId, fowVisionRadius, affectedByFoW, sendIfOnScreenOrDiscard,
                packageHashOverride, sendUnbatched);
            return p;
        }

        /// <summary>
        /// Unified, Riot-faithful particle spawn — mirrors the engine's single <c>BBSpellEffectCreate</c>
        /// building block (BBLuaConversionLibrary.lua:179/198, called ~1935x across S1). Riot has ONE
        /// effect-create primitive whose <c>bindObject</c> / <c>targetObject</c> / <c>pos</c> /
        /// <c>targetPos</c> / <c>orientTowards</c> inputs are all independent and optional; whether the FX
        /// is unit-bound, target-bound or position-only follows from WHICH inputs are set, not from which
        /// function you call. The older <see cref="AddParticlePos"/> / <see cref="AddParticle"/> /
        /// <see cref="AddParticleTarget"/> overloads are a LeagueSandbox split whose parameter surfaces
        /// drifted apart (e.g. AddParticlePos cannot set packageHashOverride/sendUnbatched). Prefer this
        /// for new code and migrate old callers onto it.
        ///
        /// <para>Flag derivation matches the engine (replay + S1: scripts pass <c>Flags = 0</c>, the engine
        /// derives the wire bits):</para>
        /// <list type="bullet">
        /// <item><c>UpdateOrientation</c> is set automatically whenever <paramref name="orientTowards"/> is
        /// non-zero (replay: 100% of oriented FX carry it) — callers no longer have to remember it.</item>
        /// <item><c>SimulateWhileOffScreen</c> is the baseline default (replay: 98% of FX groups).</item>
        /// </list>
        ///
        /// <para>Riot params deliberately NOT exposed here are client-only render hints absent from the 4.x
        /// FX_Create wire packet (SpecificUnitToExclude, FacesTarget, PersistsThroughReconnect, TimeoutInFOW,
        /// HideFromSpectator, BindFlexToOwnerPAR) — exposing them would be silent no-ops.</para>
        /// </summary>
        /// <param name="effectName">Particle/troy name (Riot: EffectName).</param>
        /// <param name="caster">Spawning unit (Riot: Owner). Used for package hash, default KeywordNetID, team.</param>
        /// <param name="bindObject">Unit the FX is attached to (Riot: BindObject). Takes priority over targetObject for positioning.</param>
        /// <param name="targetObject">Secondary bind/target unit (Riot: TargetObject).</param>
        /// <param name="pos">Spawn position when no bind/target unit (Riot: Pos).</param>
        /// <param name="targetPos">Aim/end position (Riot: TargetPos).</param>
        /// <param name="orientTowards">Facing direction (Riot: OrientTowards); non-zero auto-sets UpdateOrientation.</param>
        /// <param name="boneName">Attach bone on the bind object (Riot: BoneName).</param>
        /// <param name="targetBoneName">Attach bone on the target object (Riot: TargetBoneName).</param>
        /// <param name="scale">Effect scale (Riot: Scale).</param>
        /// <param name="lifetime">Seconds the particle should live (server-side; Riot uses SpellEffectRemove).</param>
        /// <param name="specificTeamOnly">Only team that may see the FX (Riot: SpecificTeamOnly).</param>
        /// <param name="specificUnitOnly">Only unit that may see the FX (Riot: SpecificUnitOnly).</param>
        /// <param name="specificUnitExclude">Unit barred from seeing the FX; everyone else sees it under normal FoW (Riot: SpecificUnitToExclude).</param>
        /// <param name="flags">FX flags (Riot: Flags). UpdateOrientation is OR'd in automatically when oriented.</param>
        /// <param name="followsGroundTilt">Tilt the FX along ground slope (Riot: FollowsGroundTilt).</param>
        /// <param name="sendIfOnScreenOrDiscard">One-shot: only sent to recipients who can see it at creation (Riot: SendIfOnScreenOrDiscard).</param>
        /// <param name="fowVisibilityRadius">Fog-of-war reveal radius (Riot: FOWVisibilityRadius).</param>
        /// <param name="fowTeam">Which team's fog-of-war gates the FX (Riot: FOWTeam). TEAM_NEUTRAL = never gated (always shown); any other team = gated. Currently acted on as an on/off toggle; raw value stored on the particle.</param>
        /// <param name="effectNameForEnemy">Alternate troy shown to the enemy team (Riot: EffectNameForOtherTeam).</param>
        /// <param name="keywordObject">Object the FX inherits keyword/material overrides from (Riot: KeywordObject;
        /// wire KeywordNetID = its NetId). Leave null for the replay-verified default of 0 (most FX, incl. altars,
        /// send 0); pass the caster/owner explicitly only for the minority of spells that do so (e.g. Xerath).</param>
        /// <param name="packageHashOverride">Overrides the FX_Create_Group package hash (sound-bank resolution).</param>
        /// <param name="sendUnbatched">Send as its own FX_Create_Group packet (required for sound-carrying FX).</param>
        /// <param name="ignoreCasterVisibility">Show even when the caster is not visible to the recipient.</param>
        /// <param name="overrideTargetHeight">Additive height offset applied to the FX position.</param>
        /// <param name="netId">Explicit NetID (0 = auto-assign).</param>
        /// <param name="startFromTime">Spawn the FX pre-aged by this many seconds (Riot: StartFromTime /
        /// SpellEffectCreateRecord.m_StartFromTime). The client fast-forwards the particle simulation by this
        /// amount on create, so a persistent/looping effect appears already running instead of playing its
        /// spawn burst. Travels on the wire as FXCreateData.TimeSpent. Default 0 = play from birth.</param>
        public static Particle SpellEffectCreate(
            string effectName,
            GameObject caster = null,
            GameObject bindObject = null,
            GameObject targetObject = null,
            Vector2? pos = null,
            Vector2? targetPos = null,
            Vector3 orientTowards = new Vector3(),
            string boneName = "",
            string targetBoneName = "",
            float scale = 1.0f,
            float lifetime = 1.0f,
            TeamId specificTeamOnly = TeamId.TEAM_ALL,
            GameObject specificUnitOnly = null,
            GameObject specificUnitExclude = null,
            FXFlags flags = FXFlags.SimulateWhileOffScreen,
            bool followsGroundTilt = false,
            bool sendIfOnScreenOrDiscard = false,
            float fowVisibilityRadius = 0f,
            TeamId fowTeam = TeamId.TEAM_UNKNOWN,
            string effectNameForEnemy = null,
            GameObject keywordObject = null,
            uint? packageHashOverride = null,
            bool sendUnbatched = false,
            bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f,
            uint netId = 0,
            // Riot SpellEffectCreateRecord.m_StartFromTime: spawn the FX pre-aged by this many seconds
            // (client fast-forwards the particle sim). Default 0 = play from birth as before.
            float startFromTime = 0f)
        {
            // Engine-faithful flag derivation: the client only orients the FX when this bit is set,
            // and Riot's engine sets it whenever an orientation vector is supplied (see summary).
            var resolvedFlags = flags;
            if (orientTowards.Length() > 0)
            {
                resolvedFlags |= FXFlags.UpdateOrientation;
            }

            // Riot's KeywordObject -> wire KeywordNetID (its NetId). Replay-verified default is 0 (the packet
            // builder turns a null override into 0). Pass keywordObject only for the minority of spells that
            // set it explicitly (e.g. Xerath beam/tar); leave it null otherwise.
            uint? keywordNetIDOverride = keywordObject?.NetId;

            // Riot's FOWTeam: a real team => the FX is fog-of-war gated; TEAM_NEUTRAL => always shown.
            // We currently act on it via the on/off IsAffectedByFoW toggle; the raw team is stored below
            // (default TEAM_UNKNOWN keeps the historical "gated" default).
            bool affectedByFoW = fowTeam != TeamId.TEAM_NEUTRAL;

            // One primitive, dispatched to the matching ctor by which inputs are present — identical
            // position/bind resolution to the legacy overloads, just with the full parameter surface.
            Particle particle;
            if (targetObject != null)
            {
                particle = new Particle(_game, caster, bindObject, targetObject, effectName, scale,
                    boneName, targetBoneName, netId, orientTowards, followsGroundTilt, lifetime,
                    specificTeamOnly, specificUnitOnly, resolvedFlags, ignoreCasterVisibility,
                    overrideTargetHeight, effectNameForEnemy, keywordNetIDOverride, fowVisibilityRadius,
                    affectedByFoW, sendIfOnScreenOrDiscard, packageHashOverride, sendUnbatched, specificUnitExclude,
                    startFromTime);
            }
            else if (bindObject != null)
            {
                particle = new Particle(_game, caster, bindObject, targetPos ?? pos ?? bindObject.Position,
                    effectName, scale, boneName, targetBoneName, netId, orientTowards, followsGroundTilt,
                    lifetime, specificTeamOnly, specificUnitOnly, resolvedFlags, ignoreCasterVisibility,
                    overrideTargetHeight, effectNameForEnemy, keywordNetIDOverride, fowVisibilityRadius,
                    affectedByFoW, sendIfOnScreenOrDiscard, packageHashOverride, sendUnbatched, specificUnitExclude,
                    startFromTime);
            }
            else
            {
                var startPos = pos ?? caster?.Position ?? Vector2.Zero;
                var endPos = targetPos ?? startPos;
                particle = new Particle(_game, caster, startPos, endPos, effectName, scale, boneName,
                    targetBoneName, netId, orientTowards, followsGroundTilt, lifetime, specificTeamOnly,
                    specificUnitOnly, resolvedFlags, ignoreCasterVisibility, overrideTargetHeight,
                    effectNameForEnemy, keywordNetIDOverride, fowVisibilityRadius, affectedByFoW,
                    sendIfOnScreenOrDiscard, packageHashOverride, sendUnbatched, specificUnitExclude,
                    startFromTime);
            }

            // Capture the raw FOWTeam for fidelity (no packet/audience effect today; gating is via
            // affectedByFoW above). Safe post-construction since nothing reads it during the build.
            particle.FOWTeam = fowTeam;
            return particle;
        }

        /// <summary>
        /// Removes the specified particle from ObjectManager and networks the removal to players.
        /// </summary>
        /// <param name="p">Particle to remove.</param>
        public static void RemoveParticle(Particle p)
        {
            if (p != null)
            {
                p.SetToRemove();
            }
        }

        /// <summary>
        /// Creates a new Minion with the specified parameters.
        /// </summary>
        /// <param name="owner">AI unit that owns this minion.</param>
        /// <param name="model">Internal name of the model of this minion.</param>
        /// <param name="name">Internal name of the minion.</param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="skinId">ID of the skin the minion should use for its model.</param>
        /// <param name="ignoreCollision">Whether or not the minion should ignore collisions.</param>
        /// <param name="targetable">Whether or not the minion should be targetable.</param>
        /// <param name="targetingFlags">Flags determining targetability.</param>
        /// <param name="visibilityOwner">Specifies the only unit which should be able to see the minion.</param>
        /// <param name="isVisible">Whether or not this minion should be visible.</param>
        /// <param name="aiPaused">Whether or not this minion's AI is inactive.</param>
        /// <returns>New Minion instance.</returns>
        // Mirrors Riot's SpawnMinion BuildingBlock (BBLuaConversionLibrary.lua:227, 4.20-verified):
        // SpawnMinion(Name, Skin, AiScript, Pos, Team, Stunned, Rooted, Silenced, Invulnerable, MagicImmune,
        // IgnoreCollision, GoldRedirectTarget). `model`=Name, `skinId`=Skin (4.20 wire SkinID is an int),
        // and the spawn-time status flags + AiScript + gold-redirect now map 1:1. `name`/`targetable`/
        // `targetingFlags`/`visibilityOwner`/`isVisible`/`aiPaused`/`useSpells`/`spawnBitfieldExtra` are
        // LeagueSandbox plumbing. There is deliberately NO `isWard` param — like Riot, ward-ness is
        // data-driven (a unit IS a ward iff its CharData carries the `Ward` UnitTag; see Minion ctor /
        // reference_unit_tags_model). New params default to the prior behaviour (no AI = EmptyAIScript =
        // Riot idle.lua; no status; no redirect), so existing callers are unaffected.
        public static Minion AddMinion
        (
            ObjAIBase owner,
            string model,
            string name,
            Vector2 position,
            TeamId team = TeamId.TEAM_NEUTRAL,
            int skinId = 0,
            bool ignoreCollision = false,
            bool targetable = true,
            SpellDataFlags targetingFlags = 0,
            ObjAIBase visibilityOwner = null,
            bool isVisible = true,
            bool aiPaused = true,
            bool useSpells = true,
            byte spawnBitfieldExtra = 0,
            // --- Riot SpawnMinion parity ---
            string aiScript = "",
            bool rooted = false,
            bool stunned = false,
            bool silenced = false,
            bool invulnerable = false,
            bool magicImmune = false,
            AttackableUnit goldRedirectTarget = null
        )
        {
            var m = new Minion(_game, owner, position, model, name, 0, team, skinId, ignoreCollision, targetable, visibilityOwner, enableScripts: useSpells, AIScript: aiScript);
            m.Stats.IsTargetableToTeam = targetingFlags;
            // Spawn-time status (Riot SpawnMinion Stunned/Rooted/Silenced/Invulnerable/MagicImmune): set
            // BEFORE AddObject so the unit's initial replicated Status carries them (no 1-frame window of
            // it un-rooted/targetable). These are PERMANENT spawn properties with no backing buff — e.g. a
            // stationary box/ward/turret is Rooted so it attacks in place and never chases. SetStatus only
            // mutates internal state + recomputes (no packet), so it is safe before the object is added.
            // M2 Phase 3: these spawn-time CC properties are capability disables (Riot has no Stunned/Rooted/
            // Silenced flag): root = can't move, stun = can't move/attack/cast, silence = can't cast.
            if (rooted) m.SetStatus(StatusFlags.CanMove, false);
            if (stunned) m.SetStatus(StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast, false);
            if (silenced) m.SetStatus(StatusFlags.CanCast, false);
            if (invulnerable) m.SetStatus(StatusFlags.Invulnerable, true);
            if (magicImmune) m.SetStatus(StatusFlags.MagicImmune, true);
            // Set the extra-bitfield byte BEFORE AddObject — AddObject synchronously broadcasts
            // the spawn packet via the visibility pipeline, so any setter called after returns
            // here would only mutate server state and never reach the wire.
            m.SpawnBitfieldExtra = spawnBitfieldExtra;
            _game.ObjectManager.AddObject(m);
            if (owner != null)
            {
                m.SetVisibleByTeam(owner.Team, isVisible);
            }
            // GoldRedirectTarget (Riot SpawnMinion GoldRedirectObj): set AFTER AddObject — assigning it
            // broadcasts PKT_UpdateGoldRedirectTarget (0x7), which references the minion's NetId, so the
            // unit must already be spawned. Only when provided (the setter always emits a packet).
            if (goldRedirectTarget != null)
            {
                m.GoldRedirectTarget = goldRedirectTarget;
            }

            m.PauseAI(aiPaused);
            return m;
        }

        /// <summary>
        /// Spawns a <see cref="Marker"/> — a lightweight position-only entity broadcast
        /// via <c>SpawnMarkerS2C</c> (packet id 0x100). No model, no animation, no
        /// minimap presence. Use as an anchor NetID for particle <c>BindNetID</c> /
        /// <c>TargetNetID</c> fields (e.g. beam endpoints). Replay-verified pattern:
        /// Vel'Koz R uses 4 of these for the beam's endpoint anchor.
        /// </summary>
        /// <param name="position">World position (X, Y=height, Z). Y is preserved for
        /// <see cref="Marker.GetHeight"/> rather than being recomputed from terrain.</param>
        /// <param name="visibilitySize">Visibility radius in world units (replay default 100).</param>
        /// <param name="team">Team the marker is associated with. <see cref="TeamId.TEAM_NEUTRAL"/>
        /// makes it globally visible (subject to FoW).</param>
        public static Marker AddMarker(Vector2 position, float visibilitySize = 100f, TeamId team = TeamId.TEAM_NEUTRAL)
        {
            var marker = new Marker(_game, position, visibilitySize, netNodeId: 0x40, team: team);
            _game.ObjectManager.AddObject(marker);
            // Client's obj_AI_Marker::Create hardcodes the team to TEAM_NEUTRAL. To make
            // FX particles bound to the marker render correctly, broadcast the actual team
            // immediately after the spawn — Riot does this (a6db3774 replay, t=510997
            // sequence: SpawnMarkerS2C → S2C_UnitChangeTeam → S2C_MoveMarker → FX_Create_Group).
            if (team != TeamId.TEAM_NEUTRAL)
            {
                _game.PacketNotifier.NotifySetTeam((GameObject)marker);
            }
            // OnEnterVisibilityClient + OnEnterLocalVisibilityClient initialize the marker
            // as a fully-formed obj_AI_Base on the client. Without these the marker is
            // half-spawned and FX particles using BindNetID=marker fail to render.
            // Replay-verified Riot pattern.
            _game.PacketNotifier.NotifyMarkerVisibilityInit(marker);
            return marker;
        }

        /// <summary>
        /// Creates a stationary perception bubble at the given location.
        /// </summary>
        /// <param name="position">Position to spawn the perception bubble at.</param>
        /// <param name="radius">Size of the perception bubble.</param>
        /// <param name="duration">Number of seconds the perception bubble should exist.</param>
        /// <param name="team">Team the perception bubble should be owned by.</param>
        /// <param name="revealStealthed">Whether or not the perception bubble should reveal stealthed units while they are in range.</param>
        /// <param name="revealSpecificUnitOnly">Specific unit to reveal. Perception bubble will not reveal any other units when used. *NOTE* Currently does nothing.</param>
        /// <param name="collisionArea">Area around the perception bubble where units are not allowed to move into.</param>
        ///
        /// <returns>New Region instance.</returns>
        public static Region AddPosPerceptionBubble
        (
            Vector2 position,
            float radius,
            float duration,
            TeamId team = TeamId.TEAM_NEUTRAL,
            bool revealStealthed = false,
            AttackableUnit revealSpecificUnitOnly = null,
            float collisionArea = 0f,
            RegionType regionType = RegionType.Circle,
            bool ignoresLoS = false
        )
        {
            return new Region
            (
                _game, team, position, regionType,
                visionTarget: revealSpecificUnitOnly,
                visionRadius: radius,
                revealStealth: revealStealthed,
                collisionRadius: collisionArea,
                lifetime: duration,
                onlyShowTarget: revealSpecificUnitOnly != null,
                ignoresLoS: ignoresLoS
            );
        }

        /// <summary>
        /// Creates a perception bubble which is attached to a specific unit.
        /// </summary>
        /// <param name="target">Unit to attach the perception bubble to.</param>
        /// <param name="radius">Size of the perception bubble.</param>
        /// <param name="duration">Number of seconds the perception bubble should exist.</param>
        /// <param name="team">Team the perception bubble should be owned by.</param>
        /// <param name="revealStealthed">Whether or not the perception bubble should reveal stealthed units while they are in range.</param>
        /// <param name="revealSpecificUnitOnly">Specific unit to reveal. Perception bubble will not reveal any other units when used. *NOTE* Currently does nothing.</param>
        /// <param name="collisionArea">Area around the perception bubble where units are not allowed to move into.</param>
        ///
        /// <returns>New Region instance.</returns>
        public static Region AddUnitPerceptionBubble
        (
            AttackableUnit target,
            float radius,
            float duration,
            TeamId team = TeamId.TEAM_NEUTRAL,
            bool revealStealthed = false,
            AttackableUnit revealSpecificUnitOnly = null,
            float collisionArea = 0f,
            RegionType regionType = RegionType.Circle,
            bool ignoresLoS = false,
            bool onlyShowTarget = true
        )
        {
            return new Region
            (
                _game, team, target.Position, regionType,
                collisionUnit: target,
                visionTarget: revealSpecificUnitOnly,
                visionRadius: radius,
                revealStealth: revealStealthed,
                collisionRadius: collisionArea,
                lifetime: duration,
                ignoresLoS: ignoresLoS,
                onlyShowTarget: onlyShowTarget
            );
        }

        /// <summary>
        /// Prints the specified string to the in-game chat.
        /// </summary>
        /// <param name="msg">String to print.</param>
        public static void PrintChat(string msg)
        {
            _game.PacketNotifier
                .NotifyS2C_SystemMessage(msg); // TODO: Move PacketNotifier usage to less abstract classes
        }

        /// <summary>
        /// Broadcasts a message to all players on a specific team.
        /// </summary>
        /// <param name="team">Team to send message to (BLUE or PURPLE).</param>
        /// <param name="msg">Message to send (supports HTML formatting).</param>
        public static void PrintChatTeam(TeamId team, string msg)
        {
            _game.PacketNotifier.NotifyS2C_SystemMessage(team, msg);
        }

        /// <summary>
        /// Broadcasts a message to all players except those on the specified team.
        /// </summary>
        /// <param name="excludedTeam">Team to exclude from the broadcast.</param>
        /// <param name="msg">Message to send (supports HTML formatting).</param>
        public static void PrintChatAllExceptTeam(TeamId excludedTeam, string msg)
        {
            var enemyTeam = excludedTeam == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE;
            _game.PacketNotifier.NotifyS2C_SystemMessage(enemyTeam, msg);
        }

        /// <summary>
        /// Sends a chat message formatted like a player message to all teams.
        /// Shows proper team colors (green for allies, red for enemies).
        /// </summary>
        /// <param name="senderName">Name of the sender (champion name).</param>
        /// <param name="senderModel">Model name of the sender (e.g., "Ezreal").</param>
        /// <param name="senderTeam">Team of the sender.</param>
        /// <param name="message">Message content.</param>
        /// <param name="isAllChat">Whether this is an all-chat message or team-only.</param>
        public static void PrintPlayerChat(string senderName, string senderModel, TeamId senderTeam, string message,
            bool isAllChat = true)
        {
            var ownTeamColor = "#00FF00"; // Green
            var enemyTeamColor = "#FF0000"; // Red
            var enemyTeam = senderTeam == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE;

            // Format: "[All] Name (Model): message"
            var formattedMessage = $"{senderName} ({senderModel}): </font><font color=\"#FFFFFF\">{message}";

            if (isAllChat)
            {
                // Send green message to own team
                var ownTeamMsg = $"<font color=\"{ownTeamColor}\">[All] " + formattedMessage;
                _game.PacketNotifier.NotifyS2C_SystemMessage(senderTeam, ownTeamMsg);

                // Send red message to enemy team
                var enemyTeamMsg = $"<font color=\"{enemyTeamColor}\">[All] " + formattedMessage;
                _game.PacketNotifier.NotifyS2C_SystemMessage(enemyTeam, enemyTeamMsg);
            }
            else
            {
                // Team-only chat
                var teamMsg = $"<font color=\"{ownTeamColor}\">[Team] " + formattedMessage;
                _game.PacketNotifier.NotifyS2C_SystemMessage(senderTeam, teamMsg);
            }
        }

        /// <summary>
        /// Checks if the AttackableUnit is within the specified range of a target position.
        /// </summary>
        /// <param name="unit">Unit to check.</param>
        /// <param name="targetPos">Position to check from.</param>
        /// <param name="range">Range around the position to check.</param>
        /// <param name="isAlive">Whether or not the unit should be alive.</param>
        /// <returns></returns>
        public static bool IsUnitInRange(AttackableUnit unit, Vector2 targetPos, float range, bool isAlive)
        {
            if (unit.IsDead == isAlive)
            {
                return false;
            }

            return GameServerCore.Extensions.IsVectorWithinRange(unit.Position, targetPos, range);
        }

        /// <summary>
        /// Acquires all dead or alive AttackableUnits within the specified range of a target position.
        /// </summary>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <returns>List of AttackableUnits.</returns>
        public static IEnumerable<AttackableUnit> EnumerateUnitsInRange(Vector2 targetPos, float range, bool isAlive)
        {
            foreach (var obj in _game.Map.CollisionHandler.GetNearestObjects(
                         new System.Activities.Presentation.View.Circle(targetPos, range)))
            {
                if (obj is AttackableUnit u && (!isAlive || !u.IsDead))
                {
                    yield return u;
                }
            }

            // Turrets and buildings are intentionally excluded from the dynamic QuadTree (their footprints live in the navgrid),
            // so they need to be mixed in here via a separate linear scan or targeting AIs would never see them.
            foreach (var obj in _game.Map.CollisionHandler.EnumerateStaticTargetsInRange(targetPos, range))
            {
                if (obj is AttackableUnit u && (!isAlive || !u.IsDead))
                {
                    yield return u;
                }
            }
        }

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within range and filters them by IsValidTarget using the given flags.
        ///     Mirrors Riot's plain BBForEachUnitInTargetArea: the visibility gate is masked, so
        ///     fog-hidden/stealthed units are included. Use <see cref="ForEachVisibleUnitInTargetArea"/>
        ///     for the visibility-checked acquisition variant.
        /// </summary>
        /// <param name="self">Unit used as the source for team/flag checks.</param>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered enumerable of AttackableUnits.</returns>
        public static IEnumerable<AttackableUnit> EnumerateValidUnitsInRange(
            AttackableUnit self,
            Vector2 targetPos,
            float range,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            return EnumerateValidUnitsInRangeImpl(self, targetPos, range, isAlive, useFlags | SpellDataFlags.IgnoreVisibilityCheck);
        }

        // Shared body (Riot: `anonymous namespace'::ForEachUnitInTargetAreaImpl(..., extraFlags) —
        // public entry points funnel here, differing only in whether IgnoreVisibilityCheck was ORed).
        private static IEnumerable<AttackableUnit> EnumerateValidUnitsInRangeImpl(
            AttackableUnit self,
            Vector2 targetPos,
            float range,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            return EnumerateUnitsInRange(targetPos, range, isAlive)
                .Where(unit => ValidTargetCheck(self, unit, useFlags));
        }

        #region Riot BB target-iteration family
        // 1:1 ports of the S1 server's Lua BuildingBlock target iterators
        // (luabuildingblockhelper.cpp), names minus the BB prefix. Shared shape per Riot:
        // circle/box spatial query → TargetHelper::ValidTargetCheck per unit (the plain variants
        // OR IgnoreVisibilityCheck; the *Visible* variants keep the visibility gate active) →
        // FilterByBuffName → shape-specific pick. Dead units are not pre-filtered — Riot gates
        // them purely on AffectDead inside ValidTargetCheck.

        /// <summary>
        /// Riot BBForEachUnitInTargetArea: every valid unit in the circle, unsorted (grid order).
        /// Hits fog-hidden/stealthed units (visibility masked like all plain BB iterators).
        /// </summary>
        /// <param name="attacker">Caster used for team/flag/visibility checks (AttackerVar).</param>
        /// <param name="center">Circle center (CenterVar).</param>
        /// <param name="range">Circle radius (Range).</param>
        /// <param name="flags">SpellDataFlags filter (Flags).</param>
        /// <param name="buffNameFilter">When set, filters by buff presence (BuffNameFilter).</param>
        /// <param name="inclusiveBuffFilter">true = only units WITH the buff, false = only units
        /// WITHOUT it (InclusiveBuffFilter; Riot FilterByBuffName).</param>
        public static List<AttackableUnit> ForEachUnitInTargetArea(
            AttackableUnit attacker, Vector2 center, float range, SpellDataFlags flags,
            string buffNameFilter = null, bool inclusiveBuffFilter = true)
        {
            return CollectUnitsInTargetArea(attacker, center, range,
                flags | SpellDataFlags.IgnoreVisibilityCheck, buffNameFilter, inclusiveBuffFilter);
        }

        /// <summary>Riot BBForEachVisibleUnitInTargetArea: like <see cref="ForEachUnitInTargetArea"/>
        /// but fog-hidden/stealthed units are excluded (visibility gate active).</summary>
        public static List<AttackableUnit> ForEachVisibleUnitInTargetArea(
            AttackableUnit attacker, Vector2 center, float range, SpellDataFlags flags,
            string buffNameFilter = null, bool inclusiveBuffFilter = true)
        {
            return CollectUnitsInTargetArea(attacker, center, range, flags, buffNameFilter, inclusiveBuffFilter);
        }

        /// <summary>
        /// Riot BBForNClosestUnitsInTargetArea: up to <paramref name="maximumUnitsToPick"/> valid
        /// units, closest to <paramref name="center"/> first (Riot partial_sorts by 2D distance,
        /// WhichUnitIsCloserToPoint). Visibility masked.
        /// </summary>
        public static List<AttackableUnit> ForNClosestUnitsInTargetArea(
            AttackableUnit attacker, Vector2 center, float range, int maximumUnitsToPick, SpellDataFlags flags,
            string buffNameFilter = null, bool inclusiveBuffFilter = true)
        {
            return PickNClosest(CollectUnitsInTargetArea(attacker, center, range,
                flags | SpellDataFlags.IgnoreVisibilityCheck, buffNameFilter, inclusiveBuffFilter),
                center, maximumUnitsToPick);
        }

        /// <summary>Riot BBForNClosestVisibleUnitsInTargetArea: like
        /// <see cref="ForNClosestUnitsInTargetArea"/> but fog-hidden/stealthed units are never
        /// acquired (S1 uses this for foxfire/box/chain target acquisition).</summary>
        public static List<AttackableUnit> ForNClosestVisibleUnitsInTargetArea(
            AttackableUnit attacker, Vector2 center, float range, int maximumUnitsToPick, SpellDataFlags flags,
            string buffNameFilter = null, bool inclusiveBuffFilter = true)
        {
            return PickNClosest(CollectUnitsInTargetArea(attacker, center, range, flags, buffNameFilter, inclusiveBuffFilter),
                center, maximumUnitsToPick);
        }

        /// <summary>
        /// Riot BBForEachUnitInTargetAreaRandom: up to <paramref name="maximumUnitsToPick"/> valid
        /// units picked in random order without replacement. Visibility masked.
        /// </summary>
        public static List<AttackableUnit> ForEachUnitInTargetAreaRandom(
            AttackableUnit attacker, Vector2 center, float range, int maximumUnitsToPick, SpellDataFlags flags,
            string buffNameFilter = null, bool inclusiveBuffFilter = true)
        {
            return PickNRandom(CollectUnitsInTargetArea(attacker, center, range,
                flags | SpellDataFlags.IgnoreVisibilityCheck, buffNameFilter, inclusiveBuffFilter),
                maximumUnitsToPick);
        }

        /// <summary>Riot BBForEachVisibleUnitInTargetAreaRandom: like
        /// <see cref="ForEachUnitInTargetAreaRandom"/> but visibility-checked.</summary>
        public static List<AttackableUnit> ForEachVisibleUnitInTargetAreaRandom(
            AttackableUnit attacker, Vector2 center, float range, int maximumUnitsToPick, SpellDataFlags flags,
            string buffNameFilter = null, bool inclusiveBuffFilter = true)
        {
            return PickNRandom(CollectUnitsInTargetArea(attacker, center, range, flags, buffNameFilter, inclusiveBuffFilter),
                maximumUnitsToPick);
        }

        /// <summary>
        /// Riot BBForEachUnitInTargetRectangle: every valid unit intersecting the box centered on
        /// <paramref name="center"/>, whose length axis points from the attacker toward the center
        /// (S1: Normalize(Center − AttackerPos)). Bounding-radius-aware intersection
        /// (TargetHelper::IsObjectIntersectingBox). Visibility masked.
        /// </summary>
        /// <param name="halfWidth">Half extent across the axis (HalfWidth).</param>
        /// <param name="halfLength">Half extent along the axis (HalfLength).</param>
        public static List<AttackableUnit> ForEachUnitInTargetRectangle(
            AttackableUnit attacker, Vector2 center, float halfWidth, float halfLength, SpellDataFlags flags,
            string buffNameFilter = null, bool inclusiveBuffFilter = true)
        {
            return CollectUnitsInTargetRectangle(attacker, center, halfWidth, halfLength,
                flags | SpellDataFlags.IgnoreVisibilityCheck, buffNameFilter, inclusiveBuffFilter);
        }

        /// <summary>Riot BBForEachVisibleUnitInTargetRectangle: like
        /// <see cref="ForEachUnitInTargetRectangle"/> but visibility-checked.</summary>
        public static List<AttackableUnit> ForEachVisibleUnitInTargetRectangle(
            AttackableUnit attacker, Vector2 center, float halfWidth, float halfLength, SpellDataFlags flags,
            string buffNameFilter = null, bool inclusiveBuffFilter = true)
        {
            return CollectUnitsInTargetRectangle(attacker, center, halfWidth, halfLength, flags,
                buffNameFilter, inclusiveBuffFilter);
        }

        private static List<AttackableUnit> CollectUnitsInTargetArea(
            AttackableUnit attacker, Vector2 center, float range, SpellDataFlags flags,
            string buffNameFilter, bool inclusiveBuffFilter)
        {
            var units = new List<AttackableUnit>();
            foreach (var unit in EnumerateUnitsInRange(center, range, false))
            {
                if (ValidTargetCheck(attacker, unit, flags)
                    && PassesBuffNameFilter(unit, buffNameFilter, inclusiveBuffFilter))
                {
                    units.Add(unit);
                }
            }
            return units;
        }

        private static List<AttackableUnit> CollectUnitsInTargetRectangle(
            AttackableUnit attacker, Vector2 center, float halfWidth, float halfLength, SpellDataFlags flags,
            string buffNameFilter, bool inclusiveBuffFilter)
        {
            // Length axis: from the attacker toward the box center (S1 rect impl normalizes
            // Center − AttackerPos). Zero-guard: fall back to the attacker's facing.
            var axis = center - attacker.Position;
            if (axis.LengthSquared() <= float.Epsilon)
            {
                axis = new Vector2(attacker.Direction.X, attacker.Direction.Z);
                if (axis.LengthSquared() <= float.Epsilon)
                {
                    axis = new Vector2(1.0f, 0.0f);
                }
            }
            axis = Vector2.Normalize(axis);

            var units = new List<AttackableUnit>();
            // Broadphase: half-diagonal plus slack for unit bounding radii; the exact
            // bounding-radius-aware box test below decides.
            float broadphase = MathF.Sqrt(halfWidth * halfWidth + halfLength * halfLength) + 400.0f;
            foreach (var unit in EnumerateUnitsInRange(center, broadphase, false))
            {
                if (IsUnitIntersectingBox(unit, center, axis, halfWidth, halfLength)
                    && ValidTargetCheck(attacker, unit, flags)
                    && PassesBuffNameFilter(unit, buffNameFilter, inclusiveBuffFilter))
                {
                    units.Add(unit);
                }
            }
            return units;
        }

        /// <summary>
        /// Riot TargetHelper::IsObjectIntersectingBox: circle-vs-oriented-box with the unit's
        /// bounding radius; exact corner test when the unit center is outside both extents.
        /// </summary>
        private static bool IsUnitIntersectingBox(AttackableUnit unit, Vector2 center, Vector2 axis, float halfWidth, float halfLength)
        {
            float radius = unit.CollisionRadius;
            var dv = unit.Position - center;
            float along = MathF.Abs(dv.X * axis.X + dv.Y * axis.Y) - halfLength;
            if (along > radius)
            {
                return false;
            }
            float across = MathF.Abs(dv.X * -axis.Y + dv.Y * axis.X) - halfWidth;
            if (across > radius)
            {
                return false;
            }
            if (along <= 0.0f || across <= 0.0f)
            {
                return true;
            }
            return radius * radius >= along * along + across * across;
        }

        // Riot FilterByBuffName: empty filter = no-op; inclusive keeps only units WITH the buff,
        // exclusive only units WITHOUT it. Non-ObjAIBase units bypass the filter (kept).
        private static bool PassesBuffNameFilter(AttackableUnit unit, string buffNameFilter, bool inclusive)
        {
            if (string.IsNullOrEmpty(buffNameFilter) || unit is not ObjAIBase)
            {
                return true;
            }
            return unit.HasBuff(buffNameFilter) == inclusive;
        }

        // Riot: std::partial_sort with WhichUnitIsCloserToPoint (2D distance to center, ascending),
        // then the first MaximumUnitsToPick.
        private static List<AttackableUnit> PickNClosest(List<AttackableUnit> units, Vector2 center, int maximumUnitsToPick)
        {
            units.Sort((a, b) =>
                Vector2.DistanceSquared(a.Position, center).CompareTo(Vector2.DistanceSquared(b.Position, center)));
            if (units.Count > maximumUnitsToPick)
            {
                units.RemoveRange(maximumUnitsToPick, units.Count - maximumUnitsToPick);
            }
            return units;
        }

        // Riot: random pick without replacement (GRandomGen index % remaining) up to the cap.
        private static List<AttackableUnit> PickNRandom(List<AttackableUnit> units, int maximumUnitsToPick)
        {
            int picks = Math.Min(maximumUnitsToPick, units.Count);
            for (int i = 0; i < picks; i++)
            {
                int j = i + _random.Next(units.Count - i);
                (units[i], units[j]) = (units[j], units[i]);
            }
            if (units.Count > picks)
            {
                units.RemoveRange(picks, units.Count - picks);
            }
            return units;
        }

        /// <summary>
        /// Riot BBForEachUnitInTargetAreaAddBuff: fused area query + buff application — Riot's
        /// aura tick primitive (S1 users: BeaconAura/BeaconAuraAP = the Rally banner,
        /// OdinVanguardAura = Map8 relic; the aura pulses a short RENEW_EXISTING buff onto every
        /// valid unit in range each tick). Returns the affected units. Riot's BuffAddType /
        /// BuffType / BuffMaxStack / TickRate / IsHiddenOnClient call-site params come from the
        /// C# buff script's BuffScriptMetaData in our engine instead. Visibility masked.
        /// </summary>
        /// <param name="buffAttacker">Buff source (BuffAttackerVar); defaults to the attacker.</param>
        public static List<AttackableUnit> ForEachUnitInTargetAreaAddBuff(
            AttackableUnit attacker, Vector2 center, float range, SpellDataFlags flags,
            string buffName, float buffDuration, byte buffNumberOfStacks = 1,
            ObjAIBase buffAttacker = null, Spell originSpell = null,
            string buffNameFilter = null, bool inclusiveBuffFilter = true,
            VariableTable variableTable = null)
        {
            var units = CollectUnitsInTargetArea(attacker, center, range,
                flags | SpellDataFlags.IgnoreVisibilityCheck, buffNameFilter, inclusiveBuffFilter);
            AddBuffToEach(units, buffName, buffDuration, buffNumberOfStacks,
                buffAttacker ?? attacker as ObjAIBase, originSpell, variableTable);
            return units;
        }

        /// <summary>Riot BBForEachVisibleUnitInTargetAreaAddBuff: like
        /// <see cref="ForEachUnitInTargetAreaAddBuff"/> but visibility-checked.</summary>
        public static List<AttackableUnit> ForEachVisibleUnitInTargetAreaAddBuff(
            AttackableUnit attacker, Vector2 center, float range, SpellDataFlags flags,
            string buffName, float buffDuration, byte buffNumberOfStacks = 1,
            ObjAIBase buffAttacker = null, Spell originSpell = null,
            string buffNameFilter = null, bool inclusiveBuffFilter = true,
            VariableTable variableTable = null)
        {
            var units = CollectUnitsInTargetArea(attacker, center, range, flags, buffNameFilter, inclusiveBuffFilter);
            AddBuffToEach(units, buffName, buffDuration, buffNumberOfStacks,
                buffAttacker ?? attacker as ObjAIBase, originSpell, variableTable);
            return units;
        }

        private static void AddBuffToEach(
            List<AttackableUnit> units, string buffName, float buffDuration, byte buffNumberOfStacks,
            ObjAIBase buffAttacker, Spell originSpell, VariableTable variableTable)
        {
            foreach (var unit in units)
            {
                AddBuff(buffName, buffDuration, buffNumberOfStacks, originSpell, unit, buffAttacker,
                    variableTable: variableTable);
            }
        }

        /// <summary>
        /// Riot BBForEachPetInTarget: every pet belonging to the given unit, optionally filtered
        /// by buff presence (FilterByBuffName semantics: inclusive = only pets WITH the buff).
        /// No area/visibility dimension — Riot walks the owner's pet slots directly. No S1 spell
        /// script uses it; ported for API-family completeness.
        /// </summary>
        public static List<Pet> ForEachPetInTarget(
            AttackableUnit target, string buffNameFilter = null, bool inclusiveBuffFilter = true)
        {
            var pets = new List<Pet>();
            foreach (var obj in _game.ObjectManager.GetObjects().Values)
            {
                if (obj is Pet pet && pet.Owner == target
                    && PassesBuffNameFilter(pet, buffNameFilter, inclusiveBuffFilter))
                {
                    pets.Add(pet);
                }
            }
            return pets;
        }

        /// <summary>
        /// Riot BBForEachPointOnLine: <paramref name="iterations"/> points along the segment of
        /// total length <paramref name="size"/>, centered on <paramref name="center"/> pushed
        /// <paramref name="pushForward"/> units toward <paramref name="faceTowardsPos"/>. Riot
        /// steps size/iterations per point from the segment's back edge (first point one step in,
        /// last point on the front edge) and only yields points on VALID nav cells.
        /// </summary>
        public static List<Vector2> ForEachPointOnLine(
            Vector2 center, Vector2 faceTowardsPos, float size, float pushForward, int iterations)
        {
            var points = new List<Vector2>();
            if (iterations <= 0)
            {
                return points;
            }

            var dir = faceTowardsPos - center;
            if (dir.LengthSquared() <= float.Epsilon)
            {
                return points;
            }
            dir = Vector2.Normalize(dir);

            var pos = center + dir * (pushForward - size * 0.5f);
            var step = dir * (size / iterations);
            for (int i = 0; i < iterations; i++)
            {
                pos += step;
                if (_game.Map.PathingHandler.IsWalkable(pos))
                {
                    points.Add(pos);
                }
            }
            return points;
        }

        /// <summary>
        /// Riot BBForEachPointAroundCircle: points on the circle around
        /// <paramref name="center"/>, starting at angle 0 (= center + (radius, 0)). Riot's angle
        /// step is 360 / iterations in INTEGER degrees — e.g. 7 iterations yields a 51° step and
        /// therefore 8 points; quirk preserved. No nav-cell gate (unlike the line variant).
        /// </summary>
        public static List<Vector2> ForEachPointAroundCircle(Vector2 center, float radius, int iterations)
        {
            var points = new List<Vector2>();
            if (iterations <= 0)
            {
                return points;
            }

            int stepDegrees = 360 / iterations;
            for (int angle = 0; angle < 360; angle += stepDegrees)
            {
                float rad = angle * (MathF.PI / 180.0f);
                points.Add(center + radius * new Vector2(MathF.Cos(rad), MathF.Sin(rad)));
            }
            return points;
        }

        /// <summary>
        /// Riot BBDestroyMissileForTarget: flags every in-flight missile whose target is the given
        /// unit for destruction — regardless of owner or team. This is the untargetability
        /// pattern: S1 users are VladimirSanguinePool, ZhonyasRingShield, ShenStandUnited and
        /// VoidWalk (Kassadin R), which drop incoming targeted missiles on becoming untargetable.
        /// </summary>
        public static void DestroyMissileForTarget(AttackableUnit target)
        {
            foreach (var obj in _game.ObjectManager.GetObjects().Values)
            {
                if (obj is SpellMissile missile && !missile.IsToRemove() && missile.TargetUnit == target)
                {
                    missile.SetToRemove();
                }
            }
        }

        /// <summary>
        /// Riot BBCanSeeTarget(Viewer, Target): whether the viewer's side currently sees the
        /// target (fog + stealth). Same primitive as ValidTargetCheck's visibility gate. S1 users:
        /// Brand passive missile, Garen E, Swain R.
        /// </summary>
        public static bool CanSeeTarget(AttackableUnit viewer, AttackableUnit target)
        {
            return target.IsVisibleByTeam(viewer.Team);
        }

        /// <summary>
        /// Buff types a dispel treats as negative. Riot's BuffManager::DispellNegative body is not
        /// decompiled (client stub asserts); this classification is inferred from the BuffType
        /// taxonomy — every control/impairment type plus the debuff categories. QSS (the only S1
        /// user) advertises "removes all debuffs" INCLUDING suppression, so SUPPRESSION is in.
        /// </summary>
        private static readonly HashSet<BuffType> NegativeBuffTypes = new()
        {
            BuffType.COMBAT_DEHANCER, BuffType.STUN, BuffType.SILENCE, BuffType.TAUNT,
            BuffType.POLYMORPH, BuffType.SLOW, BuffType.SNARE, BuffType.DAMAGE, BuffType.SLEEP,
            BuffType.NEAR_SIGHT, BuffType.FEAR, BuffType.CHARM, BuffType.POISON,
            BuffType.SUPPRESSION, BuffType.BLIND, BuffType.SHRED, BuffType.FLEE,
            BuffType.KNOCKUP, BuffType.KNOCKBACK, BuffType.DISARM
        };

        /// <summary>
        /// Riot BBDispellNegativeBuffs (QSS active): removes every dispellable negative buff from
        /// the target. Buffs whose script sets IsNonDispellable (Riot scriptBaseBuff
        /// mNonDispellable / kSpellFlagNonDispellable) survive.
        /// </summary>
        public static void DispellNegativeBuffs(AttackableUnit target)
        {
            // GetBuffs() returns the live buff list — copy before deactivating mutates it.
            foreach (var buff in target.GetBuffs().ToArray())
            {
                if (NegativeBuffTypes.Contains(buff.BuffType)
                    && !(buff.BuffScript?.BuffMetaData?.IsNonDispellable ?? false))
                {
                    buff.DeactivateBuff();
                }
            }
        }

        /// <summary>
        /// Riot BBSpellBuffClear: fully clears the named buff from the target — ALL instances and
        /// stacks at once (BuffInstance::Clear), unlike RemoveBuff which follows the add-type's
        /// removal rules. Riot defaults the name to the calling script; pass it explicitly here.
        /// </summary>
        public static void SpellBuffClear(AttackableUnit target, string buffName)
        {
            foreach (var buff in target.GetBuffsWithName(buffName))
            {
                buff.DeactivateBuff();
            }
        }

        /// <summary>
        /// Riot BBSetBuffCasterUnit: re-attributes a running buff to a different caster —
        /// damage/kill credit of its future ticks follows the new source. S1 users: BansheesVeil,
        /// WasStealthed, Yorick.
        /// </summary>
        public static void SetBuffCasterUnit(Buff buff, ObjAIBase caster)
        {
            buff.SourceUnit = caster;
        }

        /// <summary>
        /// Riot BBSetVoiceOverride(Target, OverrideSuffix): switches the unit's voice-over bank
        /// (Riven "Ult", Sion "Berserk"/"Max") — stores the suffix on the CharacterDataStack and
        /// mirrors it to clients via S2C_ChangeCharacterVoice (vision-gated).
        /// </summary>
        public static void SetVoiceOverride(AttackableUnit target, string overrideSuffix)
        {
            target.CharacterDataStack.VoiceOverrideSuffix = overrideSuffix ?? "";
            _game.PacketNotifier.NotifyS2C_ChangeCharacterVoice(target, overrideSuffix ?? "");
        }

        /// <summary>Resets the unit's voice-over bank to default (S2C_ChangeCharacterVoice reset bit).</summary>
        public static void ResetVoiceOverride(AttackableUnit target)
        {
            target.CharacterDataStack.VoiceOverrideSuffix = "";
            _game.PacketNotifier.NotifyS2C_ChangeCharacterVoice(target, "", true);
        }
        #endregion

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within range and filters them by IsValidTarget using the given flags.
        ///     Returns units ordered by distance to <paramref name="targetPos" />.
        /// </summary>
        /// <param name="self">Unit used as the source for team/flag checks.</param>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered, distance-sorted list of AttackableUnits.</returns>
        public static List<AttackableUnit> GetUnitsInRange(
            AttackableUnit self,
            Vector2 targetPos,
            float range,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            return EnumerateValidUnitsInRange(self, targetPos, range, isAlive, useFlags)
                .OrderBy(unit => Vector2.DistanceSquared(unit.Position, targetPos))
                .ToList();
        }

        /// <summary>
        /// Fills the spell's <c>CastInfo.Targets</c> with every valid unit in <paramref name="range"/>
        /// around <paramref name="center"/> — the port of Riot's <c>BBAdjustCastInfoCenterAOE</c>
        /// (S1 luabuildingblockhelper.cpp: area query via TargetHelper::GetSelectedUnitsCircle +
        /// SpellCastInfo::AddTarget per hit, run from the AdjustCastInfoBuildingBlocks hook).
        /// Call from <c>OnSpellPreCast</c> — it runs BEFORE the CastSpellAns notify, exactly like
        /// Riot's hook — so the ANS announces the FULL hit list (replay-verified: instant
        /// point-blank AoEs carry the full list; KatarinaW 18/173 casts with 1-4 targets, the
        /// empty rest = nobody in radius). What the CLIENT does with the list (decomp-verified):
        /// (1) per-target hit/after FX ONLY for isAutoAttack || ApplyAttackEffect spells
        /// (SpellDatabaseClient::CastSpell gate) — normal spells get NO client hit particle from
        /// it, Riot sends those explicitly (FX_Create_Group, one instance per hit target:
        /// katarina_w_tar.troy x4 on a 4-target W) → scripts KEEP their manual AddParticle loop;
        /// (2) SomeUnitsAlive cast-frame abort — targeted spells only (targeting type);
        /// (3) RevealToCastTargets — reveals the CASTER to each targeted enemy team, gated on
        /// SpellData SpellRevealsChampion (KatarinaW has it = the W-from-brush reveal).
        /// Returns the added units so the script's damage loop can iterate the SAME list
        /// instead of re-querying.
        /// Null/duplicate/pre-existing entries are skipped; HitResult is HIT_Normal (spell hits
        /// don't crit — attack crits are baked at the attack pipeline).
        /// </summary>
        /// <param name="spell">Spell whose CastInfo gets the targets.</param>
        /// <param name="center">AoE center (self-AoE: the caster's position; Tristana-R-style: the hit target's position).</param>
        /// <param name="range">Collection radius.</param>
        /// <param name="overrideFlags">Affect flags to use instead of the spell's own (0 = spell's flags).</param>
        public static List<AttackableUnit> AddCastTargetsInRange(Spell spell, Vector2 center, float range,
            SpellDataFlags overrideFlags = 0)
        {
            var flags = overrideFlags > 0 ? overrideFlags : spell.SpellData.Flags;
            var units = GetUnitsInRange(spell.CastInfo.Owner, center, range, true, flags);
            var added = new List<AttackableUnit>(units.Count);
            foreach (var unit in units)
            {
                if (unit == null || spell.CastInfo.Targets.Exists(t => t.Unit == unit))
                {
                    continue;
                }
                spell.CastInfo.Targets.Add(new CastTarget(unit, HitResult.HIT_Normal));
                added.Add(unit);
            }
            return added;
        }

        /// <summary>
        /// Alive lane minions of a team (Riot bot API GetMinions(team, laneId)). Pass <paramref name="lane"/>
        /// to restrict to one lane (LaneMinion.Lane, set at spawn from the barracks), or null for all lanes.
        /// Used by the bot tasks (TaskPushLane = own team + lane, TaskKillMinion = enemy team / all lanes).
        /// NOTE: GetObjects() copies the object map each call — fine for the handful of bots, optimise if
        /// bot counts grow.
        /// </summary>
        public static List<LaneMinion> GetMinions(TeamId team, Lane? lane = null)
        {
            var result = new List<LaneMinion>();
            foreach (var obj in _game.ObjectManager.GetObjects().Values)
            {
                if (obj is LaneMinion minion && minion.Team == team && !minion.IsDead
                    && (lane == null || minion.Lane == lane.Value))
                {
                    result.Add(minion);
                }
            }
            return result;
        }

        /// <summary>All alive+dead champions of a team (Riot bot API GetHeroes(team)).</summary>
        public static List<Champion> GetHeroes(TeamId team)
        {
            return _game.ObjectManager.GetAllChampionsFromTeam(team);
        }

        /// <summary>
        /// Alive structures (turrets + inhibitors + nexus) of a team (Riot bot API GetStructures). Used by
        /// TaskKillTower. GetObjects() copies — fine for the few bots.
        /// </summary>
        public static List<AttackableUnit> GetStructures(TeamId team)
        {
            var result = new List<AttackableUnit>();
            foreach (var obj in _game.ObjectManager.GetObjects().Values)
            {
                if ((obj is BaseTurret || obj is ObjBuilding) && obj is AttackableUnit u
                    && u.Team == team && !u.IsDead)
                {
                    result.Add(u);
                }
            }
            return result;
        }

        public static Vector2 GetMovePositionByCollisionOffset(AttackableUnit unit, AttackableUnit target, float offset = 0f, bool isBehind = false)
        {
            float overshoot = unit.CollisionRadius + target.CollisionRadius + offset;
            if (isBehind)
            {
                return target.Position - (unit.Position - target.Position).Normalized() * (!IsWalkable(target.Position.X, target.Position.Y) ? - overshoot : overshoot);
            }
            else
            {
                return target.Position + (unit.Position - target.Position).Normalized() * (!IsWalkable(target.Position.X, target.Position.Y) ? - overshoot : overshoot);
            }
        }

        /// <summary>
        /// Programmatic item purchase for a bot (Riot bot API BuyItem): gold check → add to inventory
        /// (InventoryManager.AddItem notifies the owner) → deduct gold. Returns false if unaffordable or
        /// the inventory is full. Simplified vs the player Shop flow (no recipe combining / undo stack) —
        /// fine for the bot's basic shopping list.
        /// </summary>
        public static bool BuyItem(ObjAIBase buyer, int itemId)
        {
            var data = _game.ItemManager.SafeGetItemType(itemId);
            if (data == null || buyer.Stats.Gold < data.TotalPrice)
            {
                return false;
            }
            var result = buyer.Inventory.AddItem(data, buyer);
            if (!result.Value)
            {
                return false;
            }
            buyer.Stats.Gold -= data.TotalPrice;
            return true;
        }

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within a ring and filters them by IsValidTarget using the given flags.
        /// </summary>
        /// <param name="self">Unit used as the source for team/flag checks.</param>
        /// <param name="origin">Center of the ring.</param>
        /// <param name="innerRadius">Inner radius of the ring.</param>
        /// <param name="outerRadius">Outer radius of the ring.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered enumerable of AttackableUnits.</returns>
        public static IEnumerable<AttackableUnit> EnumerateUnitsInRing(
            AttackableUnit self,
            Vector2 targetPos,
            float innerRadius,
            float outerRadius,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            innerRadius = MathF.Max(0.0f, innerRadius);
            outerRadius = MathF.Max(0.0f, outerRadius);

            if (outerRadius < innerRadius)
            {
                (innerRadius, outerRadius) = (outerRadius, innerRadius);
            }

            if (outerRadius <= 0.0f)
            {
                return Enumerable.Empty<AttackableUnit>();
            }

            var innerRadiusSquared = innerRadius * innerRadius;
            var outerRadiusSquared = outerRadius * outerRadius;

            return EnumerateValidUnitsInRange(self, targetPos, outerRadius, isAlive, useFlags)
                .Where(unit =>
                {
                    var distanceSquared = Vector2.DistanceSquared(unit.Position, targetPos);
                    return distanceSquared >= innerRadiusSquared && distanceSquared <= outerRadiusSquared;
                });
        }

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within a ring and filters them by IsValidTarget using the given flags.
        ///     Returns units ordered by distance to <paramref name="targetPos" />.
        /// </summary>
        /// <param name="self">Unit used as the source for team/flag checks.</param>
        /// <param name="origin">Center of the ring.</param>
        /// <param name="innerRadius">Inner radius of the ring.</param>
        /// <param name="outerRadius">Outer radius of the ring.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered, distance-sorted list of AttackableUnits.</returns>
        public static List<AttackableUnit> GetUnitsInRing(
            AttackableUnit self,
            Vector2 targetPos,
            float innerRadius,
            float outerRadius,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            return EnumerateUnitsInRing(self, targetPos, innerRadius, outerRadius, isAlive, useFlags)
                .OrderBy(unit => Vector2.DistanceSquared(unit.Position, targetPos))
                .ToList();
        }

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within a cone and filters them by IsValidTarget using the given flags.
        ///     The cone angle is expressed in total degrees, not half-angle degrees.
        /// </summary>
        /// <param name="self">Unit used as the source for team/flag checks.</param>
        /// <param name="origin">Origin of the cone.</param>
        /// <param name="direction">Forward direction of the cone.</param>
        /// <param name="range">Maximum cone length.</param>
        /// <param name="coneAngleDegrees">Total cone angle in degrees.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered enumerable of AttackableUnits.</returns>
        public static IEnumerable<AttackableUnit> EnumerateUnitsInCone(
            AttackableUnit self,
            Vector2 origin,
            Vector2 direction,
            float range,
            float coneAngleDegrees,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            if (range <= 0.0f || coneAngleDegrees <= 0.0f || direction.LengthSquared() <= float.Epsilon)
                return Enumerable.Empty<AttackableUnit>();

            var normalizedDirection = Vector2.Normalize(direction);
            var halfAngleRadians = MathF.PI * Math.Clamp(coneAngleDegrees, 0.0f, 360.0f) / 360.0f;
            var cosThreshold = MathF.Cos(halfAngleRadians);
            var rangeSquared = range * range;

            return EnumerateValidUnitsInRange(self, origin, range, isAlive, useFlags)
                .Where(unit =>
                {
                    var toTarget = unit.Position - origin;
                    var distanceSquared = toTarget.LengthSquared();

                    if (distanceSquared > rangeSquared) return false;
                    if (distanceSquared <= float.Epsilon) return true;

                    var normalizedToTarget = Vector2.Normalize(toTarget);
                    return Vector2.Dot(normalizedDirection, normalizedToTarget) >= cosThreshold;
                });
        }

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within a cone and filters them by IsValidTarget using the given flags.
        ///     The cone angle is expressed in total degrees, not half-angle degrees.
        ///     Returns units ordered by distance to <paramref name="origin" />.
        /// </summary>
        /// <param name="self">Unit used as the source for team/flag checks.</param>
        /// <param name="origin">Origin of the cone.</param>
        /// <param name="direction">Forward direction of the cone.</param>
        /// <param name="range">Maximum cone length.</param>
        /// <param name="coneAngleDegrees">Total cone angle in degrees.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered, distance-sorted list of AttackableUnits.</returns>
        public static List<AttackableUnit> GetUnitsInCone(
            AttackableUnit self,
            Vector2 origin,
            Vector2 direction,
            float range,
            float coneAngleDegrees,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            return EnumerateUnitsInCone(self, origin, direction, range, coneAngleDegrees, isAlive, useFlags)
                .OrderBy(unit => Vector2.DistanceSquared(unit.Position, origin))
                .ToList();
        }

        /// <summary>
        ///     Resolves the units hit by a spell directly from its SpellData targeting shape (Riot's
        ///     spelltargeting* model) instead of hardcoded geometry. Handles the STATIC-AoE targeting
        ///     types only:
        ///     <list type="bullet">
        ///         <item>Cone: CastConeAngle (HALF-angle) + CastConeDistance, emanating from the caster.</item>
        ///         <item>Area / Location: CastRadius circle around the cast point.</item>
        ///         <item>SelfAOE: CastRadius circle around the caster.</item>
        ///     </list>
        ///     Skillshots/beams/single-target (Direction, DragDirection, Target, Self, TargetOrLocation)
        ///     are missile- or script-driven and return an empty list — the caller keeps its own logic.
        /// </summary>
        /// <param name="spell">Spell whose SpellData + CastInfo (owner, target position, level) drive the shape.</param>
        /// <param name="overrideFlags">
        ///     Optional target-filter override. When null (default) the spell's OWN affect flags
        ///     (SpellData.Flags — the JSON's AffectEnemies/Heroes/Minions/Neutral/... bits) are used,
        ///     so scripts don't repeat them. Pass a value only to deliberately target a different set.
        /// </param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="overrideRadius">
        ///     Optional radius/range override. When null (default) the radius comes from SpellData
        ///     (CastConeDistance for cones, CastRadius for circles). Pass a value when the spell's own
        ///     data is wrong/corrupt for hit-detection (e.g. Riven's W: its 4.20 CastRadius is broken,
        ///     the real effect radius is 300 — see Riven patch history / wiki).
        /// </param>
        public static List<AttackableUnit> GetUnitsHitBySpell(Spell spell, SpellDataFlags? overrideFlags = null, float? overrideRadius = null, bool isAlive = true)
        {
            var caster = spell.CastInfo.Owner;
            var sd = spell.SpellData;
            var useFlags = overrideFlags ?? sd.Flags;
            var casterPos = caster.Position;
            var castPoint = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            // CastRadius is per-level (GetMultiFloat propagates a scalar to every level slot).
            int level = Math.Clamp((int)spell.CastInfo.SpellLevel, 0, sd.CastRadius.Length - 1);
            float radius = overrideRadius ?? sd.CastRadius[level];

            switch (sd.TargetingType)
            {
                case TargetingType.Cone:
                    // LockConeToPlayer => axis is the caster's facing; otherwise the cast direction.
                    // CastConeAngle is Riot's HALF-angle (cos-threshold); GetUnitsInCone wants the
                    // FULL angle, hence *2. Cone length = overrideRadius ?? CastConeDistance.
                    Vector2 coneDir = sd.LockConeToPlayer
                        ? new Vector2(caster.Direction.X, caster.Direction.Z)
                        : castPoint - casterPos;
                    return GetUnitsInCone(caster, casterPos, coneDir, overrideRadius ?? sd.CastConeDistance, sd.CastConeAngle * 2f, isAlive, useFlags);

                case TargetingType.Area:
                case TargetingType.Location:
                    return GetUnitsInRange(caster, castPoint, radius, isAlive, useFlags);

                case TargetingType.SelfAOE:
                    return GetUnitsInRange(caster, casterPos, radius, isAlive, useFlags);

                default:
                    LogDebug($"GetUnitsHitBySpell: TargetingType {sd.TargetingType} on {spell.SpellName} is not a static AoE shape " +
                             "(skillshot/beam/target/self) — resolve hits via the missile or script instead.");
                    return new List<AttackableUnit>();
            }
        }

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within a polygon and filters them by IsValidTarget using the given flags.
        ///     Polygon vertices are relative to the origin and are scaled by width (x) and length (y).
        /// </summary>
        /// <param name="self">Unit used as the source for team/flag checks.</param>
        /// <param name="origin">Origin of the polygon.</param>
        /// <param name="direction">Forward direction of the polygon.</param>
        /// <param name="width">Scale applied to each vertex's x coordinate.</param>
        /// <param name="length">Scale applied to each vertex's y coordinate.</param>
        /// <param name="polygonVertices">Polygon vertices relative to the origin.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered enumerable of AttackableUnits.</returns>
        public static IEnumerable<AttackableUnit> EnumerateUnitsInPolygon(
            AttackableUnit self,
            Vector2 origin,
            Vector2 direction,
            float width,
            float length,
            Vector2[] polygonVertices,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            width = MathF.Abs(width);
            length = MathF.Abs(length);
            if (polygonVertices == null || polygonVertices.Length < 3 || width <= 0.0f || length <= 0.0f)
            {
                return Enumerable.Empty<AttackableUnit>();
            }

            if (!TryNormalizeDirection(direction, out var normalizedDirection))
            {
                return Enumerable.Empty<AttackableUnit>();
            }

            var polygon = BuildScaledPolygon(polygonVertices, width, length, out var maxRange);
            if (polygon.Points.Count < 3 || maxRange <= 0.0f)
            {
                return Enumerable.Empty<AttackableUnit>();
            }

            var angleDir = Extensions.UnitVectorToAngle(normalizedDirection);

            return EnumerateValidUnitsInRange(self, origin, maxRange, isAlive, useFlags)
                .Where(unit =>
                {
                    var relativePos = (unit.Position - origin).Rotate(angleDir + 270f);
                    return polygon.IsInside(relativePos);
                });
        }

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within a polygon and filters them by IsValidTarget using the given flags.
        ///     Polygon vertices are relative to the origin and are scaled by width (x) and length (y).
        ///     Returns units ordered by distance to <paramref name="origin" />.
        /// </summary>
        /// <param name="self">Unit used as the source for team/flag checks.</param>
        /// <param name="origin">Origin of the polygon.</param>
        /// <param name="direction">Forward direction of the polygon.</param>
        /// <param name="width">Scale applied to each vertex's x coordinate.</param>
        /// <param name="length">Scale applied to each vertex's y coordinate.</param>
        /// <param name="polygonVertices">Polygon vertices relative to the origin.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered, distance-sorted list of AttackableUnits.</returns>
        public static List<AttackableUnit> GetUnitsInPolygon(
            AttackableUnit self,
            Vector2 origin,
            Vector2 direction,
            float width,
            float length,
            Vector2[] polygonVertices,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            return EnumerateUnitsInPolygon(self, origin, direction, width, length, polygonVertices, isAlive,
                    useFlags)
                .OrderBy(unit => Vector2.DistanceSquared(unit.Position, origin))
                .ToList();
        }

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within a polygon created by a spell and filters them by IsValidTarget
        ///     using the given flags. Targeted spells use the target's position and facing direction as the polygon origin.
        /// </summary>
        /// <param name="spell">Spell which created the polygon area.</param>
        /// <param name="width">Scale applied to each vertex's x coordinate.</param>
        /// <param name="length">Scale applied to each vertex's y coordinate.</param>
        /// <param name="polygonVertices">Polygon vertices relative to the origin.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered enumerable of AttackableUnits.</returns>
        public static IEnumerable<AttackableUnit> EnumerateUnitsInPolygon(
            Spell spell,
            float width,
            float length,
            Vector2[] polygonVertices,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            if (!TryResolvePolygonSpellContext(spell, out var self, out var origin, out var normalizedDirection))
            {
                return Enumerable.Empty<AttackableUnit>();
            }

            return EnumerateUnitsInPolygon(self, origin, normalizedDirection, width, length, polygonVertices,
                isAlive, useFlags);
        }

        /// <summary>
        ///     Acquires all dead or alive AttackableUnits within a polygon created by a spell and filters them by IsValidTarget
        ///     using the given flags. Targeted spells use the target's position and facing direction as the polygon origin.
        ///     Returns units ordered by distance to the resolved polygon origin.
        /// </summary>
        /// <param name="spell">Spell which created the polygon area.</param>
        /// <param name="width">Scale applied to each vertex's x coordinate.</param>
        /// <param name="length">Scale applied to each vertex's y coordinate.</param>
        /// <param name="polygonVertices">Polygon vertices relative to the origin.</param>
        /// <param name="isAlive">Whether to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Filtered, distance-sorted list of AttackableUnits.</returns>
        public static List<AttackableUnit> GetUnitsInPolygon(
            Spell spell,
            float width,
            float length,
            Vector2[] polygonVertices,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            if (!TryResolvePolygonSpellContext(spell, out var self, out var origin, out var normalizedDirection))
            {
                return [];
            }

            return GetUnitsInPolygon(self, origin, normalizedDirection, width, length, polygonVertices, isAlive,
                useFlags);
        }

        private static bool TryResolvePolygonSpellContext(
            Spell spell,
            out AttackableUnit self,
            out Vector2 origin,
            out Vector2 normalizedDirection
        )
        {
            self = default;
            origin = default;
            normalizedDirection = default;

            if (spell?.CastInfo?.Owner == null)
            {
                return false;
            }

            self = spell.CastInfo.Owner;
            origin = new Vector2(spell.CastInfo.SpellCastLaunchPosition.X,
                spell.CastInfo.SpellCastLaunchPosition.Z);
            var direction = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z) -
                            origin;

            if (spell.SpellData.TargetingType == TargetingType.Target
                && spell.CastInfo.Targets.Count > 0
                && (spell.CastInfo.Targets[0] as CastTarget)?.Unit is AttackableUnit targetUnit)
            {
                origin = targetUnit.Position;
                direction = new Vector2(targetUnit.Direction.X, targetUnit.Direction.Z);
            }

            if (!TryNormalizeDirection(direction, out normalizedDirection))
            {
                var ownerDirection = new Vector2(self.Direction.X, self.Direction.Z);
                if (!TryNormalizeDirection(ownerDirection, out normalizedDirection))
                {
                    normalizedDirection = new Vector2(1.0f, 0.0f);
                }
            }

            return true;
        }

        private static Polygon BuildScaledPolygon(
            IEnumerable<Vector2> polygonVertices,
            float width,
            float length,
            out float maxRange
        )
        {
            var polygon = new Polygon();
            maxRange = 0.0f;

            foreach (var vertex in polygonVertices)
            {
                var scaledVertex = new Vector2(vertex.X * width, vertex.Y * length);
                polygon.Add(scaledVertex);
                maxRange = MathF.Max(maxRange, scaledVertex.Length());
            }

            return polygon;
        }

        private static bool TryNormalizeDirection(Vector2 direction, out Vector2 normalizedDirection)
        {
            normalizedDirection = default;

            if (float.IsNaN(direction.X) || float.IsNaN(direction.Y) || direction.LengthSquared() <= float.Epsilon)
            {
                return false;
            }

            normalizedDirection = Vector2.Normalize(direction);
            return !float.IsNaN(normalizedDirection.X) && !float.IsNaN(normalizedDirection.Y);
        }

        /// <summary>
        /// Acquires the closest alive or dead AttackableUnit within the specified range of a target position
        /// and filters candidates by IsValidTarget using the provided flags. Mirrors Riot's plain
        /// BBForNClosestUnitsInTargetArea (visibility masked — fog-hidden/stealthed units included);
        /// use <see cref="ForNClosestVisibleUnitsInTargetArea"/> for the visibility-checked variant.
        /// </summary>
        /// <param name="self">Unit used as the source for team/flag checks.</param>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <param name="isAlive">Whether or not to return alive AttackableUnits.</param>
        /// <param name="useFlags">SpellDataFlags used by IsValidTarget.</param>
        /// <returns>Closest AttackableUnit.</returns>
        public static AttackableUnit GetClosestUnitInRange(
            AttackableUnit self,
            Vector2 targetPos,
            float range,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            return GetClosestUnitInRangeImpl(self, targetPos, range, isAlive, useFlags | SpellDataFlags.IgnoreVisibilityCheck);
        }

        // Shared body — GetClosestUnitInRange funnels here with IgnoreVisibilityCheck ORed;
        // for visibility-checked acquisition use ForNClosestVisibleUnitsInTargetArea.
        private static AttackableUnit GetClosestUnitInRangeImpl(
            AttackableUnit self,
            Vector2 targetPos,
            float range,
            bool isAlive,
            SpellDataFlags useFlags
        )
        {
            AttackableUnit closest = null;
            float bestDistSq = float.MaxValue;

            foreach (var unit in EnumerateUnitsInRange(targetPos, range, isAlive))
            {
                var distSq = Vector2.DistanceSquared(unit.Position, targetPos);
                if (distSq >= bestDistSq) continue;
                if (!ValidTargetCheck(self, unit, useFlags)) continue;

                bestDistSq = distSq;
                closest = unit;
                if (distSq <= float.Epsilon) break;
            }

            return closest;
        }

        /// <summary>
        /// Script-facing target predicate, equivalent to Riot's plain script/BB layer: the
        /// visibility gate is masked OFF (S1 server ORs 0x400000 into every plain
        /// BBForEachUnitInTargetArea-family query and into cone targeting; 264 of 268
        /// area-iterating S1 spell scripts use those). AoE/reactive checks therefore hit
        /// fog-hidden and stealthed units — use <see cref="ValidTargetCheck"/> (or the
        /// visibleOnly parameter of the range helpers) for the rare visibility-checked target
        /// ACQUISITION, Riot's BBForEach*Visible* variants (S1 uses them only in
        /// AhriFoxFireMissile, JackInTheBox, AhriTumbleKick, VolibearRChain).
        /// </summary>
        public static bool IsValidTarget(AttackableUnit self, AttackableUnit target, SpellDataFlags useFlags)
        {
            return ValidTargetCheck(self, target, useFlags | SpellDataFlags.IgnoreVisibilityCheck);
        }

        /// <summary>
        /// Faithful port of TargetHelper::ValidTargetCheck (4.17 mac decomp, TargetHelper.cpp)
        /// ending in the per-class IsValidSpellTarget overrides it dispatches to (obj_AI_Turret /
        /// obj_Building / obj_AI_Minion / AIHero); the S1 server corroborates the minion decision
        /// tree with symbol names. Includes the visibility gate — most script-facing paths mask
        /// it via <see cref="IsValidTarget"/>. Not ported: the AffectImportantBotTargets bot filter.
        /// </summary>
        public static bool ValidTargetCheck(AttackableUnit self, AttackableUnit target, SpellDataFlags useFlags)
        {
            // Local content workaround (not part of Riot's check): shrines are never spell targets.
            if (target.Model.Contains("Shrine"))
            {
                return false;
            }

            // Untargetable — globally or to the caster's team; GetIsTargetableToTeam folds in BOTH
            // (matters for e.g. the TT relic / its anchor: NonTargetableAll) — fails unless
            // AffectUntargetable, or AffectUseable rescues a useable object. Riot checks this
            // BEFORE AlwaysSelf, so an untargetable caster is not even self-targetable.
            if ((useFlags & SpellDataFlags.AffectUntargetable) == 0
                && !target.GetIsTargetableToTeam(self.Team)
                && !((useFlags & SpellDataFlags.AffectUseable) != 0 && target.CharData.IsUseable))
            {
                return false;
            }

            var isSelf = target == self;
            // AlwaysSelf is decisive: the caster passes regardless of the team/type/dead gates below.
            if ((useFlags & SpellDataFlags.AlwaysSelf) != 0 && isSelf)
            {
                return true;
            }
            if ((useFlags & SpellDataFlags.NotAffectSelf) != 0 && isSelf)
            {
                return false;
            }

            // Riot rejects outright when either NonTargetable* bit is set on the spell
            // (flags & 0x1800000); ValidTargetCheck's later per-team NonTargetableAlly/Enemy
            // branches are dead code behind this.
            if ((useFlags & SpellDataFlags.NonTargetableAll) != 0)
            {
                return false;
            }

            // Team gate. Note both checks apply to a neutral-team caster hitting a neutral
            // target: it needs AffectFriends (same team) AND AffectNeutral.
            var sameTeam = target.Team == self.Team;
            var isNeutralTeam = target.Team == TeamId.TEAM_NEUTRAL;
            if ((sameTeam && (useFlags & SpellDataFlags.AffectFriends) == 0)
                || (!sameTeam && !isNeutralTeam && (useFlags & SpellDataFlags.AffectEnemies) == 0)
                || (isNeutralTeam && (useFlags & SpellDataFlags.AffectNeutral) == 0))
            {
                return false;
            }

            if ((useFlags & SpellDataFlags.AffectDead) == 0 && target.IsDead)
            {
                return false;
            }

            // NotAffectZombie: Riot hosts this inside the hero override (only champions zombie);
            // kept type-independent here — same behavior.
            if ((useFlags & SpellDataFlags.NotAffectZombie) != 0 && target.IsZombie)
            {
                return false;
            }

            // Visibility gate — Riot: !(flags & IgnoreVisibilityCheck) && !obj->IsVisibleTo(caster)
            // → false (evaluated after the type gate there; pure conjunction, so order-equivalent).
            // Governs click-target cast validation (SpellTargeting* with mIgnoreVisibility=false)
            // and the BBForEach*Visible* acquisition variants; the plain script layer masks it.
            if ((useFlags & SpellDataFlags.IgnoreVisibilityCheck) == 0 && !target.IsVisibleByTeam(self.Team))
            {
                return false;
            }

            // Per-class type gate — Riot's virtual obj->IsValidSpellTarget(team, casterID, flags).
            switch (target)
            {
                case Champion:
                    return (useFlags & SpellDataFlags.AffectHeroes) != 0;

                // obj_AI_Turret returns AffectTurrets and nothing else — buildings are NOT turrets.
                case BaseTurret:
                    return (useFlags & SpellDataFlags.AffectTurrets) != 0;

                // obj_AI_Minion (covers lane minions, jungle monsters, pets, wards, clones).
                case Minion minion:
                    // "Treated as hero" record branch — clones. 4.17 additionally rejects on
                    // bit 31 (our IgnoreClones; see the SpellDataFlags doc for that divergence).
                    if (minion is Pet clonePet && clonePet.IsClone)
                    {
                        return (useFlags & SpellDataFlags.AffectHeroes) != 0
                            && (useFlags & SpellDataFlags.IgnoreClones) == 0;
                    }
                    if (minion is Pet && (useFlags & SpellDataFlags.AffectNotPet) != 0)
                    {
                        return false;
                    }
                    if ((sameTeam && (useFlags & SpellDataFlags.IgnoreAllyMinion) != 0)
                        || (!sameTeam && (useFlags & SpellDataFlags.IgnoreEnemyMinion) != 0))
                    {
                        return false;
                    }
                    // Decisive barracks-spawned tests: obj_AI_Minion RETURNS the lane-minion bit
                    // here, bypassing AffectMinions. "AffectBarracksOnly" therefore means
                    // "barracks-spawned lane minions only" — NOT inhibitors (dampeners are
                    // obj_Building and only ever gated by AffectBuildings).
                    if ((useFlags & SpellDataFlags.IgnoreLaneMinion) != 0)
                    {
                        return !(minion is LaneMinion);
                    }
                    if ((useFlags & SpellDataFlags.AffectBarracksOnly) != 0)
                    {
                        return minion is LaneMinion;
                    }
                    // Wards need AffectWards to pass and still fall through to AffectMinions.
                    if (minion.IsWard && (useFlags & SpellDataFlags.AffectWards) == 0)
                    {
                        return false;
                    }
                    return (useFlags & SpellDataFlags.AffectMinions) != 0;

                // obj_Building (HQ / inhibitors / barracks): AffectBuildings only.
                case ObjBuilding:
                    return (useFlags & SpellDataFlags.AffectBuildings) != 0;

                // Riot's obj_AI_Base base impl is assert(false) — every real target type has an
                // override; anything else is not a spell target.
                default:
                    return false;
            }
        }

        /// <summary>
        /// Enumerates all alive or dead Champions within the specified range of a target position.
        /// Compatibility overload without ally/enemy/neutral filtering.
        /// </summary>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <param name="isAlive">Whether dead champions should be excluded.</param>
        /// <returns>Enumerable of Champions.</returns>
        public static IEnumerable<Champion> EnumerateChampionsInRange(Vector2 targetPos, float range, bool isAlive)
        {
            return EnumerateUnitsInRange(targetPos, range, isAlive)
                .OfType<Champion>();
        }

        /// <summary>
        /// Acquires all alive or dead Champions within the specified range of a target position.
        /// Compatibility overload without ally/enemy/neutral filtering.
        /// </summary>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <param name="isAlive">Whether dead champions should be excluded.</param>
        /// <returns>List of Champions.</returns>
        public static List<Champion> GetChampionsInRange(Vector2 targetPos, float range, bool isAlive)
        {
            return EnumerateChampionsInRange(targetPos, range, isAlive)
                .ToList();
        }

        /// <summary>
        /// Acquires all alive or dead Champions within the specified range of a target position,
        /// ordered by distance to <paramref name="targetPos" />.
        /// Compatibility overload without ally/enemy/neutral filtering.
        /// </summary>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <param name="isAlive">Whether dead champions should be excluded.</param>
        /// <returns>Distance-sorted list of Champions.</returns>
        public static List<Champion> GetChampionsInRangeSorted(Vector2 targetPos, float range, bool isAlive)
        {
            return EnumerateChampionsInRange(targetPos, range, isAlive)
                .OrderBy(champion => Vector2.DistanceSquared(champion.Position, targetPos))
                .ToList();
        }

        /// <summary>
        /// Acquires all alive or dead Champions within the specified range of a target position.
        /// Team filtering is relative to <paramref name="self" />.
        /// </summary>
        /// <param name="self">Unit used as the source for ally/enemy filtering.</param>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <param name="isAlive">Whether dead champions should be excluded.</param>
        /// <param name="getAllies">Include allied champions.</param>
        /// <param name="getEnemies">Include enemy champions.</param>
        /// <param name="getNeutral">Include neutral-team champions.</param>
        /// <returns>List of Champions.</returns>
        public static List<Champion> GetChampionsInRange(
            AttackableUnit self,
            Vector2 targetPos,
            float range,
            bool isAlive,
            bool getAllies = true,
            bool getEnemies = true,
            bool getNeutral = true
        )
        {
            return EnumerateChampionsInRange(self, targetPos, range, isAlive, getAllies, getEnemies, getNeutral)
                .ToList();
        }

        /// <summary>
        /// Acquires all alive or dead Champions within the specified range of a target position,
        /// filtered by team relative to <paramref name="self" /> and ordered by distance to
        /// <paramref name="targetPos" />.
        /// </summary>
        /// <param name="self">Unit used as the source for ally/enemy filtering.</param>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <param name="isAlive">Whether dead champions should be excluded.</param>
        /// <param name="getAllies">Include allied champions.</param>
        /// <param name="getEnemies">Include enemy champions.</param>
        /// <param name="getNeutral">Include neutral-team champions.</param>
        /// <returns>Filtered, distance-sorted list of Champions.</returns>
        public static List<Champion> GetChampionsInRangeSorted(
            AttackableUnit self,
            Vector2 targetPos,
            float range,
            bool isAlive,
            bool getAllies = true,
            bool getEnemies = true,
            bool getNeutral = true
        )
        {
            return EnumerateChampionsInRange(self, targetPos, range, isAlive, getAllies, getEnemies, getNeutral)
                .OrderBy(champion => Vector2.DistanceSquared(champion.Position, targetPos))
                .ToList();
        }

        /// <summary>
        /// Enumerates all alive or dead Champions within the specified range of a target position.
        /// Team filtering is relative to <paramref name="self" />.
        /// </summary>
        /// <param name="self">Unit used as the source for ally/enemy filtering.</param>
        /// <param name="targetPos">Origin of the range to check.</param>
        /// <param name="range">Range to check from the target position.</param>
        /// <param name="isAlive">Whether dead champions should be excluded.</param>
        /// <param name="getAllies">Include allied champions.</param>
        /// <param name="getEnemies">Include enemy champions.</param>
        /// <param name="getNeutral">Include neutral-team champions.</param>
        /// <returns>Enumerable of champions matching range, alive-state, and team filters.</returns>
        public static IEnumerable<Champion> EnumerateChampionsInRange(
            AttackableUnit self,
            Vector2 targetPos,
            float range,
            bool isAlive,
            bool getAllies = true,
            bool getEnemies = true,
            bool getNeutral = true
        )
        {
            if (!getAllies && !getEnemies && !getNeutral)
            {
                return Enumerable.Empty<Champion>();
            }

            return EnumerateChampionsInRange(targetPos, range, isAlive)
                .Where(champion =>
                    IsTeamFilterMatch(self, champion.Team, getAllies, getEnemies, getNeutral));
        }

        public static bool CheckChampionsInRangeFromTeam(Vector2 checkPos, float range, TeamId team,
            bool isAlive = false)
        {
            return _game.ObjectManager.CheckChampionsInRangeFromTeam(checkPos, range, team, isAlive);
        }

        private static bool IsTeamFilterMatch(
            AttackableUnit self,
            TeamId candidateTeam,
            bool getAllies,
            bool getEnemies,
            bool getNeutral
        )
        {
            if (candidateTeam == TeamId.TEAM_NEUTRAL)
            {
                return getNeutral;
            }

            if (self == null)
            {
                // Without a reference team, non-neutral units are included when either side is requested.
                return getAllies || getEnemies;
            }

            return candidateTeam == self.Team ? getAllies : getEnemies;
        }

        /// <summary>
        /// Counts the number of units attacking a specified GameObject of type AttackableUnit.
        /// </summary>
        /// <param name="target">AttackableUnit potentially being attacked.</param>
        /// <returns>Number of units attacking target.</returns>
        public static int CountUnitsAttackingUnit(AttackableUnit target)
        {
            return _game.ObjectManager.CountUnitsAttackingUnit(target);
        }

        /// <summary>
        /// Returns a new list of all players in the game.
        /// Players are designated as clients, this includes bot champions.
        /// Currently only a single champion is designated to each player.
        /// </summary>
        /// <returns></returns>
        public static List<Champion> GetAllPlayers()
        {
            var toreturn = new List<Champion>();
            foreach (var player in _game.PlayerManager.GetPlayers(true))
            {
                toreturn.Add(player.Champion);
            }

            return toreturn;
        }

        //Consider changing this to take bots into account too
        public static List<Champion> GetAllPlayersFromTeam(TeamId team)
        {
            return _game.ObjectManager.GetAllChampionsFromTeam(team);
        }

        /// <summary>
        /// Returns a new list of all champions in the game.
        /// </summary>
        /// <returns></returns>
        public static List<Champion> GetAllChampions()
        {
            return _game.ObjectManager.GetAllChampions();
        }

        /// <summary>
        /// Returns all live missiles owned by the given unit that originate from the named
        /// spell. Use this to find a cast's missile from a DIFFERENT script (e.g. a recast
        /// spell locating the missile spawned by the original cast) instead of holding a
        /// cross-script field reference — the engine's missile registry + the missile's
        /// SpellOrigin link is the proper cast→missile relationship.
        /// </summary>
        public static List<SpellMissile> GetMissilesByOwnerAndSpell(ObjAIBase owner, string spellName)
        {
            return _game.ObjectManager.GetObjects().Values
                .OfType<SpellMissile>()
                .Where(m => !m.IsToRemove()
                            && m.SpellOrigin?.CastInfo?.Owner == owner
                            && m.SpellOrigin?.SpellName == spellName)
                .ToList();
        }

        /// <summary>
        /// Instantly cancels any dashes the specified unit is performing.
        /// </summary>
        /// <param name="unit">Unit to stop dashing.</param>
        public static void CancelForceMovement(AttackableUnit unit)
        {
            // Allow the user to move the champion
            unit.SetForceMovementState(false);
        }

        /// <summary>
        /// Issues an engine AI order to a unit — Riot's <c>BBIssueOrder</c> (params WhomToOrderVar /
        /// Order / TargetOfOrderVar), which routes through the SAME order pipeline as player input
        /// (<c>obj_AI_Base::IssueOrder</c>; HandleMove performs the identical calls for right-clicks).
        /// The S1 block always orders onto a UNIT — corpus-wide the third param is TargetOfOrderVar,
        /// there is no point variant — hence the unit-only signature. Canonical use: post-hit attack
        /// orders (Pantheon W LeapBash, Lee Sin Q2, WW R, Talon Cutthroat all issue AI_ATTACKTO on
        /// the victim after the spell connects, so the caster immediately starts basic-attacking).
        /// </summary>
        /// <param name="whom">Unit receiving the order (BBIssueOrder <c>WhomToOrderVar</c>).</param>
        /// <param name="order">Order to issue. The values the S1 scripts use: AttackTo (AI_ATTACKTO),
        /// MoveTo (AI_MOVETO — walks to the target unit's current position), Hold (AI_HOLD) and
        /// OrderNone (AI_ORDER_NONE — clears target and order).</param>
        /// <param name="targetOfOrder">Unit the order acts on (BBIssueOrder <c>TargetOfOrderVar</c>);
        /// required for AttackTo/MoveTo, ignored for Hold/OrderNone.</param>
        public static void IssueOrder(ObjAIBase whom, OrderType order, AttackableUnit targetOfOrder = null)
        {
            if (whom == null || whom.IsDead)
            {
                return;
            }

            switch (order)
            {
                case OrderType.AttackTo:
                    if (targetOfOrder == null || targetOfOrder.IsDead)
                    {
                        return;
                    }
                    // Mirror of HandleMove's AttackTo case: order, target, then invalidate the
                    // chase-repath throttle so the unit re-paths toward the target NOW instead of
                    // finishing its previous path first.
                    whom.UpdateMoveOrder(OrderType.AttackTo, true);
                    whom.SetTargetUnit(targetOfOrder);
                    whom.ForceChaseRepath();
                    break;
                case OrderType.MoveTo:
                {
                    if (targetOfOrder == null)
                    {
                        return;
                    }
                    var path = _game.Map.PathingHandler.GetPath(whom, targetOfOrder.Position);
                    if (path == null)
                    {
                        return;
                    }
                    // Mirror of HandleMove's MoveTo case: order, waypoints, clear the attack target.
                    whom.UpdateMoveOrder(OrderType.MoveTo, true);
                    whom.SetWaypoints(path);
                    whom.SetTargetUnit(null);
                    break;
                }
                case OrderType.OrderNone:
                    whom.SetTargetUnit(null, true);
                    whom.UpdateMoveOrder(OrderType.OrderNone, true);
                    break;
                default:
                    // Hold and anything else: state-only order, current target untouched
                    // (HandleMove's Hold case keeps the target for attack-in-place).
                    whom.UpdateMoveOrder(order, true);
                    break;
            }
        }

        // ===================================================================================
        // Consolidated forced-movement verb set (docs/FORCED_MOVEMENT_REWRITE_PLAN.md P3/P4). Named after
        // Riot's building-blocks (BBMove / BBMoveAway / BBMoveToUnit) with a Force* prefix instead of BB*
        // (bare Move/MoveTo is taken by normal pathing movement here):
        //   ForceMove        ≙ BBMove        (line-path force-move to a point; self-dash / leap / knockup-with-gravity)
        //   ForceMoveAway    ≙ BBMoveAway    (push the target away from a source point — knockback)
        //   ForceMoveToUnit  ≙ BBMoveToUnit  (follow a moving unit)
        // A knockup is just ForceMove with gravity > 0 (Riot has no BBKnockup) — no separate verb.
        // Over the two engine primitives (line-path = ServerForceLinePath, follow-unit-path = ServerForceFollowUnitPath).
        // Replay-verified (2026-06-15): Riot wires knockback, knockup, dash and follow-dash ALL via the
        // engine force-move (0x64 WaypointGroupWithSpeed) with gravity — the flat SetPosition-lerp
        // BBKnockback is vestigial for SR, so ForceMoveAway is a force-move too. These replace the old
        // `ForceMovement(...)` overloads (deleted P4) — ForceMove/ForceMoveToUnit expose the full
        // primitive surface (gravity, resolve/ForceMovementType, keepFacing, lockActions, orders,
        // moveBackBy, travelTime, ignoreTerrain), so no separate raw escape hatch is needed.
        // ===================================================================================

        /// <summary>
        /// Moves <paramref name="target"/> AWAY from <paramref name="source"/> by <paramref name="distance"/>
        /// units at <paramref name="speed"/> — the engine force-move knockback (Riot's <c>BBMoveAway</c>;
        /// replay-verified as how SR knockbacks are wired, NOT the vestigial SetPosition-lerp). A negative
        /// <paramref name="distance"/> points back toward the source (a pull). <paramref name="gravity"/>
        /// &gt; 0 adds the small vertical arc most knockbacks carry (grav ~5–20 in replays);
        /// <paramref name="resolve"/> = <c>FIRST_COLLISION_HIT</c> clamps the push at a wall (wall-stun,
        /// e.g. Vayne Condemn / Sion E; publishes OnCollisionTerrain when it clamps).
        /// </summary>
        /// <param name="target">Unit being knocked.</param>
        /// <param name="source">Point to move away from (Riot BBMoveAway <c>AwayFromVar</c> = the attacker).</param>
        /// <param name="distance">Units to travel away from source (negative = toward = pull).</param>
        /// <param name="speed">Force-move speed in units/second.</param>
        /// <param name="gravity">Arc gravity; 0 = flat.</param>
        /// <param name="resolve">Destination-resolution mode (ForceMovementType) — FIRST_COLLISION_HIT for wall-stuns.</param>
        /// <param name="facing">FACE_MOVEMENT_DIRECTION vs KEEP_CURRENT_FACING (Riot MovementOrdersFacing).</param>
        /// <param name="orders">What happens to the target's order when the knockback ends.</param>
        /// <param name="movementName">Identifier surfaced in OnMoveBegin/End events.</param>
        /// <param name="awayFrom">Optional position to push away FROM, overriding <paramref name="source"/>'s live
        /// position for the DIRECTION only (source is still used for immunity/attribution). Pass this when the
        /// source has moved onto the target by the time of the knockback (e.g. Alistar W: the caster charges onto
        /// the target, so its live position is degenerate — supply the cast-origin instead). null = derive from
        /// source.Position (the default).</param>
        /// <param name="innerDistance">Riot BBMoveAway <c>DistanceInner</c>: minimum displacement floor. If terrain
        /// resolution stops the push closer than this, it is re-extended to innerDistance along the push direction
        /// (to a walkable point only). 0 = no floor (fully clampable, e.g. Headbutt). Use for a guaranteed minimum
        /// knockback (e.g. SweepingBlow's [550, 600] band: distance 600, innerDistance 550).</param>
        public static void ForceMoveAway(AttackableUnit target, AttackableUnit source, float distance, float speed,
            float gravity = 0f, ForceMovementType resolve = ForceMovementType.FURTHEST_WITHIN_RANGE,
            ForceMovementOrdersFacing facing = ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
            ForceMovementOrdersType orders = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER, string movementName = "",
            Vector2? awayFrom = null, float innerDistance = 0f)
        {
            if (target == null || source == null || speed <= 0f)
            {
                return;
            }

            var anchor = awayFrom ?? source.Position;
            var dir = target.Position - anchor;
            if (dir == Vector2.Zero)
            {
                dir = new Vector2(0, 1);
            }
            dir = Vector2.Normalize(dir);

            // Negative distance points back toward the source (pull); positive points away (knockback).
            var endPos = target.Position + dir * distance;
            bool keepFacing = facing == ForceMovementOrdersFacing.KEEP_CURRENT_FACING;
            target.ServerForceLinePath(endPos, speed, gravity, keepFacing, true, movementName, source,
                movementType: resolve, movementOrdersType: orders, innerDistance: innerDistance);
        }

        // NOTE: there is intentionally NO KnockUp verb. Riot has no BBKnockup — a knockup IS just a
        // BBMove with gravity > 0 (verified: Pulverize/Renekton/Jarvan/Wukong all call BBMove with
        // Gravity; e.g. Pulverize Speed=10, Gravity=20). So knockups use ForceMove(..., gravity: X)
        // directly; the airborne CC marker stays the caller's named buff.

        /// <summary>
        /// Self-dash / leap of <paramref name="unit"/> to <paramref name="dest"/>. Engine line-path.
        /// <paramref name="gravity"/> &gt; 0 makes it an arc (a knockup-style hop WITHOUT the CC buff).
        /// </summary>
        /// <param name="unit">Unit performing the dash.</param>
        /// <param name="dest">Destination (used when resolve = FURTHEST_WITHIN_RANGE / FIRST_WALL_HIT).</param>
        /// <param name="speed">Force-move speed in units/second.</param>
        /// <param name="gravity">Arc gravity; 0 = flat ground dash.</param>
        /// <param name="resolve">Destination-resolution mode (ForceMovementType).</param>
        /// <param name="facing">FACE_MOVEMENT_DIRECTION vs KEEP_CURRENT_FACING (Riot MovementOrdersFacing).</param>
        /// <param name="lockActions">Disable move/attack/cast during the dash (true = a "considered-CC"
        /// dash). False lets the unit act mid-dash (e.g. Akali R kill-dash).</param>
        /// <param name="ignoreTerrain">Skip the terrain-exit clamp (e.g. blink-style dashes).</param>
        /// <param name="orders">What happens to the unit's order when the dash ends.</param>
        /// <param name="movementName">Identifier surfaced in OnMoveBegin/End events.</param>
        /// <param name="idealDistance">Riot BBMove <c>IdealDistance</c>: when &gt; 0, travel exactly this many units
        /// along the direction to <paramref name="dest"/> instead of the geometric distance to it (aim = dest,
        /// length = idealDistance). 0 = use the full distance to dest.</param>
        /// <param name="moveBackBy">Riot BBMove <c>MoveBackBy</c>: pull the endpoint back toward the start by this
        /// many units (positive = stop short, negative = overshoot).</param>
        public static void ForceMove(AttackableUnit unit, Vector2 dest, float speed, float gravity = 0f,
            ForceMovementType resolve = ForceMovementType.FURTHEST_WITHIN_RANGE,
            ForceMovementOrdersFacing facing = ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION,
            bool lockActions = true, bool ignoreTerrain = false,
            ForceMovementOrdersType orders = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER,
            string movementName = "", float idealDistance = 0f, float moveBackBy = 0f)
        {
            if (unit == null || speed <= 0f)
            {
                return;
            }

            bool keepFacing = facing == ForceMovementOrdersFacing.KEEP_CURRENT_FACING;
            unit.ServerForceLinePath(dest, speed, gravity, keepFacing, lockActions, movementName, unit,
                ignoreTerrain, movementType: resolve, movementOrdersType: orders,
                idealDistance: idealDistance, moveBackBy: moveBackBy);
        }

        /// <summary>
        /// Force-move of <paramref name="unit"/> that follows a (possibly moving) <paramref name="target"/>,
        /// re-targeting each tick. Engine follow-unit-path (the real "dash to moving unit" — Lee Sin Q2,
        /// Skarner R). <paramref name="moveBackBy"/> stops short of/behind the target;
        /// <paramref name="travelTime"/> &gt; 0 gives fixed-time arrival.
        /// </summary>
        /// <param name="unit">Unit performing the dash.</param>
        /// <param name="target">Unit to follow.</param>
        /// <param name="speed">Force-move speed in units/second.</param>
        /// <param name="moveBackBy">Riot BBMoveToUnit <c>MoveBackBy</c>: distance to stop short of (positive) / behind (negative) the target.</param>
        /// <param name="travelTime">Max seconds to follow before stopping (0 = until reached / max distance).</param>
        /// <param name="followMaxDistance">Max distance to follow before giving up (0 = unlimited).</param>
        /// <param name="gravity">Arc gravity; 0 = flat.</param>
        /// <param name="facing">FACE_MOVEMENT_DIRECTION vs KEEP_CURRENT_FACING (Riot MovementOrdersFacing).</param>
        /// <param name="lockActions">Disable move/attack/cast during the dash. False lets the unit act
        /// mid-dash (e.g. Thresh's lantern-pull dash).</param>
        /// <param name="orders">What happens to the unit's order when the dash ends.</param>
        /// <param name="movementName">Identifier surfaced in OnMoveBegin/End events.</param>
        public static void ForceMoveToUnit(ObjAIBase unit, AttackableUnit target, float speed, float moveBackBy = 0f,
            float travelTime = 0f, float followMaxDistance = 0f, float gravity = 0f,
            ForceMovementOrdersFacing facing = ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION,
            bool lockActions = true, ForceMovementOrdersType orders = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER,
            string movementName = "")
        {
            if (unit == null || target == null || speed <= 0f)
            {
                return;
            }

            bool keepFacing = facing == ForceMovementOrdersFacing.KEEP_CURRENT_FACING;
            unit.ServerForceFollowUnitPath(target, speed, gravity, keepFacing, followMaxDistance, moveBackBy,
                travelTime, lockActions, movementName, unit, orders);
        }

        // ===================================================================================
        // Parabola (arc-dash) timing/height helpers. Match the 4.17 client exactly
        // (obj_AI_Base::ComputeInitialVelocity / HandleParabolicMovement, AIBaseClient.cpp):
        //   gravityAccel = ParabolicGravity * 150        (the wire field is scaled ×150 client-side)
        //   travelTime   = pathDistance / pathSpeed      (XZ flight time; follow-dashes use a fixed time)
        //   vY0          = (endY-startY + 0.5*gravityAccel*travelTime) * 1.05
        //   ballistic per 0.01s substep: vY -= gravityAccel*dt; y += vY*dt; apex at vY=0; lands at y=ground.
        // For a flat ground dash (endY==startY) the gravity CANCELS out of the TIMES — apex and landing
        // depend only on travelTime (= dist/speed). Gravity only sets the arc HEIGHT. Useful to replace
        // hardcoded phase timers (e.g. a knockup's "start falling" / "landed" moments) with exact values.
        // ===================================================================================
        private const float ParabolaGravityScale = 150f;
        private const float ParabolaInitialVelocityFactor = 1.05f;

        /// <summary>
        /// Seconds a force-move of <paramref name="distance"/> world-units at <paramref name="speed"/>
        /// takes to reach its endpoint on the server = <paramref name="distance"/> / (<paramref name="speed"/>
        /// * <see cref="AttackableUnit.ForceMoveSpeedScale"/>). Use this to time a script effect to a dash's
        /// landing (e.g. Sion R slam) so it stays exact if the engine speed-scale changes. Gravity-agnostic:
        /// the parabolic arc height is client-side and does not affect when the unit reaches its XZ endpoint.
        /// </summary>
        public static float GetForceMoveTravelTime(float distance, float speed)
        {
            if (speed <= 0f)
            {
                return 0f;
            }
            return distance / (speed * AttackableUnit.ForceMoveSpeedScale);
        }

        /// <summary>
        /// Seconds until a flat parabola/arc dash of <paramref name="distance"/> at <paramref name="speed"/>
        /// reaches its apex (the moment vertical velocity hits 0 and the unit starts falling again).
        /// Gravity-INDEPENDENT for flat ground: = 0.525 · distance/speed.
        /// </summary>
        public static float GetParabolaApexTime(float distance, float speed)
        {
            if (speed <= 0f)
            {
                return 0f;
            }
            return ParabolaInitialVelocityFactor * 0.5f * (distance / speed);
        }

        /// <summary>
        /// Seconds until a flat parabola/arc dash of <paramref name="distance"/> at <paramref name="speed"/>
        /// lands back on the ground. Gravity-INDEPENDENT for flat ground: = 1.05 · distance/speed.
        /// </summary>
        public static float GetParabolaLandingTime(float distance, float speed)
        {
            if (speed <= 0f)
            {
                return 0f;
            }
            return ParabolaInitialVelocityFactor * (distance / speed);
        }

        /// <summary>
        /// Peak height of a flat parabola/arc dash. THIS is where <paramref name="parabolicGravity"/>
        /// (the wire value, scaled ×150 client-side) matters. = vY0² / (2·gravityAccel).
        /// </summary>
        public static float GetParabolaApexHeight(float parabolicGravity, float distance, float speed)
        {
            if (speed <= 0f || parabolicGravity <= 0f)
            {
                return 0f;
            }
            float gravityAccel = parabolicGravity * ParabolaGravityScale;
            float travelTime = distance / speed;
            float vY0 = ParabolaInitialVelocityFactor * 0.5f * gravityAccel * travelTime;
            return vY0 * vY0 / (2f * gravityAccel);
        }

        /// <summary>
        /// Forces the given object to perform the given animation.
        /// </summary>
        /// <param name="obj">Object that will play the animation.</param>
        /// <param name="animName">Internal name of an animation to play.</param>
        /// <param name="scaleTime">How fast the animation should play. Default 1x speed.</param>
        /// <param name="startProgress">Time in the animation to start at.</param>
        /// <param name="scaleSpeed">How much the speed of the GameObject should affect the animation.</param>
        /// <param name="flags">Animation flags. Refer to AnimationFlags enum.</param>
        public static void PlayAnimation(GameObject obj, string animName, float scaleTime = 1.0f, float startProgress = 0,
            float scaleSpeed = 0, AnimationFlags flags = 0)
        {
            obj.PlayAnimation(animName, scaleTime, startProgress, scaleSpeed, flags);
        }

        /// <summary>
        /// Forces the given object's current animations to pause/unpause.
        /// </summary>
        /// <param name="pause">Whether or not to pause/unpause animations.</param>
        public static void PauseAnimation(GameObject obj, bool pause)
        {
            obj.PauseAnimation(pause);
        }

        /// <summary>
        /// Forces the given object to stop performing the given animation (or optionally all animations).
        /// </summary>
        /// <param name="obj">Object who's animations will be stopped.</param>
        /// <param name="animation">Internal name of the animation to stop playing. Set blank/null if stopAll is true.</param>
        /// <param name="animation">Internal name of the animation to stop. Empty string + <c>StopAll</c> stops every track.</param>
        /// <param name="flags">Combination of <see cref="LeaguePackets.Game.Common.StopAnimationFlags"/>. Default <c>IgnoreLock</c> matches the prior bool-API default.</param>
        public static void StopAnimation(GameObject obj, string animation, LeaguePackets.Game.Common.StopAnimationFlags flags = LeaguePackets.Game.Common.StopAnimationFlags.IgnoreLock)
        {
            obj.StopAnimation(animation, flags);
        }

        public static void SealSpellSlot(ObjAIBase target, SpellSlotType slotType, int slot,
            SpellbookType spellbookType, bool seal)
        {
            slot = ConvertAPISlot(spellbookType, slotType, slot);

            if (slot == -1)
            {
                return;
            }

            if (spellbookType == SpellbookType.SPELLBOOK_SUMMONER)
            {
                target.Stats.SetSummonerSpellEnabled((byte)slot, !seal);
                return;
            }

            target.Stats.SetSpellEnabled((byte)slot, !seal);
        }

        /// <summary>
        /// Overrides the given animation with the other given animation.
        /// First string is the animation to override, second string is the animation to play in place of the first.
        /// </summary>
        /// <param name="unit">Unit to set animation states on.</param>
        /// <param name="overrideAnim">Animation to use instead.</param>
        /// <param name="toOverrideAnim">Animation to override.</param>
        /// <param name="source">The object applying the override (usually `this` in a script).</param>
        public static void OverrideAnimation(AttackableUnit unit, string overrideAnim, string toOverrideAnim,
            object source = null)
        {
            unit.SetAnimStates(new Dictionary<string, string> { { toOverrideAnim, overrideAnim } }, source);
        }

        /// <summary>
        /// Clears the given overridden animation for the specified source, falling back to the previous override in the stack or the original animation.
        /// </summary>
        /// <param name="unit">Unit to set animation states on.</param>
        /// <param name="overriddenAnim">Animation which has been overridden.</param>
        /// <param name="source">The object that applied the override.</param>
        public static void ClearOverrideAnimation(AttackableUnit unit, string overriddenAnim, object source = null)
        {
            unit.SetAnimStates(new Dictionary<string, string> { { overriddenAnim, "" } }, source);
        }

        /// <summary>
        /// Removes all animation overrides applied by a specific source.
        /// </summary>
        /// <param name="unit">Unit to clear animations on.</param>
        /// <param name="source">The object that applied the overrides.</param>
        public static void RemoveOverrideAnimations(AttackableUnit unit, object source)
        {
            unit.RemoveAnimStates(source);
        }

        // REMOVED: SetAutocast(spell) script verb. Verified misconception — NPC_SetAutocast is not a
        // script-facing "auto cast indicator" toggle; it's the engine's empowered-attack-override
        // announce (slot + critSlot, clear = 255/255) that the client HUD consumes via
        // Spellbook::SetAutocastSpellLocal. No Riot script ever calls anything like it (zero hits in
        // the S1 Lua and 4.20 Lua corpora), and the engine already announces automatically with
        // dedup + clear handling in ObjAIBase.NotifyAutocastForPreparedAttack; a raw script call
        // would bypass and desync that state tracking.

        /// <summary>
        /// Sets the state of the given status for the given unit.
        /// </summary>
        /// <param name="unit">Unit to set status.</param>
        /// <param name="status">Status to set.</param>
        /// <param name="enabled">Whether or not the status should be enabled.</param>
        public static void SetStatus(AttackableUnit unit, StatusFlags status, bool enabled)
        {
            unit.SetStatus(status, enabled);
        }

        /// <summary>
        /// Sets the given spell slot of the given unit to the spell of the given name.
        /// </summary>
        /// <param name="target">Unit to set the spell for.</param>
        /// <param name="newName">Name of the spell to place in the slot.</param>
        /// <param name="slotType">Type of slot being used.</param>
        /// <param name="slot">Index of the spell slot to change.</param>
        /// <param name="fullReplace">Whether or not the spell should be entirely replaced, or just the name. Typically used for transformations.</param>
        /// <returns>Newly created spell or existing spell with the given name. Null for failure.</returns>
        /// <param name="changeFlags">Wire bitfield of the ChangeSlotSpellData packet beyond the
        /// IsSummonerSpell bit. Riot never sends 0 — replay-verified (Sion W): 0x6E when arming a
        /// temporary override spell, 0x0E when restoring the original / plain sets (default).</param>
        public static Spell SetSpell(ObjAIBase target, string newName, SpellSlotType slotType, int slot,
            bool fullReplace = false, byte changeFlags = 0x0E)
        {
            slot = ConvertAPISlot(slotType, slot);

            if (slot == -1)
            {
                return null;
            }

            return target.SetSpell(newName, (byte)slot, true, fullReplace, changeFlags);
        }

        /// <summary>
        /// Sets the targeting type of the spell in a given slot to a given targeting type.
        /// </summary>
        /// <param name="target">Unit to set the targeting type for.</param>
        /// <param name="slotType">Type of slot being used.</param>
        /// <param name="slot">Index of the spell slot to change.</param>
        /// <param name="newType">Targeting type to set.</param>
        public static void SetTargetingType(ObjAIBase target, SpellSlotType slotType, int slot, TargetingType newType)
        {
            slot = ConvertAPISlot(slotType, slot);

            if (slot == -1)
            {
                return;
            }

            var spell = target.Spells[(short)slot];

            spell.SpellData.SetTargetingType(newType);

            if (target is Champion champion)
            {
                _game.PacketNotifier.NotifyChangeSlotSpellData
                (
                    _game.PlayerManager.GetClientInfoByChampion(champion).ClientId,
                    target,
                    (byte)slot,
                    ChangeSlotSpellDataType.TargetingType,
                    targetingType: newType
                );
            }
        }

        /// <summary>
        /// Locks (or unlocks) the camera of the player controlling <paramref name="unit"/> onto their
        /// champion — for steered/forced-movement abilities (e.g. Sion R's Unstoppable Onslaught charge:
        /// lock on cast, unlock when the charge ends). No-op for non-player units.
        /// </summary>
        /// <param name="unit">The champion whose owning player's camera is locked.</param>
        /// <param name="locked">True to lock/steer the camera, false to release it.</param>
        /// <param name="distance">Camera distance while locked (Sion R uses 900).</param>
        public static void LockCamera(ObjAIBase unit, bool locked, float distance = 900f)
        {
            if (unit is Champion champion)
            {
                var player = _game.PlayerManager.GetClientInfoByChampion(champion);
                if (player != null)
                {
                    _game.PacketNotifier.NotifyLockCamera(player, locked, distance);
                }
            }
        }

        /// <summary>
        /// Overrides a champion's level cap on its owning client (S2C_UnitSetMaxLevelOverride) — e.g. URF's
        /// level 30. The server-side cap is enforced separately via MapScriptMetadata.MaxLevel; this keeps
        /// the client HUD/XP bar correct past level 18. No-op for non-player units.
        /// </summary>
        public static void SetMaxLevelOverride(ObjAIBase unit, byte maxLevel)
        {
            if (unit is Champion champion)
            {
                var player = _game.PlayerManager.GetClientInfoByChampion(champion);
                if (player != null)
                {
                    _game.PacketNotifier.NotifyS2C_UnitSetMaxLevelOverride(player.ClientId, unit, maxLevel);
                }
            }
        }

        /// <summary>
        /// Overrides the mana cost of a spell slot on the owning client (S2C_UnitSetSpellPARCost, owner-only).
        /// This is the engine side of Riot's BBSetPARCostInc building block: recast-window ults (Ahri R,
        /// Kha'Zix R, Irelia R, ...) set a negative <paramref name="amount"/> on their R slot when cast so
        /// the recasts are free, then call it again with amount 0 to restore the base cost when the window
        /// ends; Kog'Maw's Living Artillery uses a positive, escalating amount. No-op for non-player units.
        /// </summary>
        /// <param name="unit">The casting unit (must be a player champion to have an owning client).</param>
        /// <param name="slot">Spell slot to modify (e.g. 3 for R).</param>
        /// <param name="costType">Additive or Multiplicative mana-cost increment.</param>
        /// <param name="amount">Cost delta; negative = cheaper, positive = more expensive, 0 = restore base.</param>
        public static void SetSpellPARCost(ObjAIBase unit, int slot, SpellPARCostType costType, float amount)
        {
            if (unit is Champion champion)
            {
                var player = _game.PlayerManager.GetClientInfoByChampion(champion);
                if (player != null)
                {
                    _game.PacketNotifier.NotifyS2C_UnitSetSpellPARCost(player.ClientId, unit, costType, slot, amount);
                }
            }
        }

        /// <summary>
        /// Points <paramref name="owner"/>'s hover indicator at <paramref name="target"/>
        /// (S2C_SetHoverIndicatorTarget, broadcast). The owner is the clickable object carrying the
        /// indicator (e.g. a Thresh lantern) and the target is what its "click here" ring points toward
        /// (e.g. Thresh). The indicator radius/texture come from the owner's HoverIndicator* stats.
        /// </summary>
        public static void SetHoverIndicatorTarget(AttackableUnit owner, GameObject target)
        {
            _game.PacketNotifier.NotifyS2C_SetHoverIndicatorTarget(owner, target);
        }

        /// <summary>
        /// Enables/disables <paramref name="owner"/>'s hover indicator (S2C_SetHoverIndicatorEnabled).
        /// The client gates rendering on this flag (defaults false), so it must be set true — together
        /// with <see cref="SetHoverIndicatorTarget"/> — for the "click here" ring to appear. Pass
        /// <paramref name="team"/> to restrict the indicator to one side (e.g. the Thresh lantern's
        /// allies); null sends to everyone.
        /// </summary>
        public static void SetHoverIndicatorEnabled(AttackableUnit owner, bool enabled, TeamId? team = null)
        {
            _game.PacketNotifier.NotifyS2C_SetHoverIndicatorEnabled(owner, enabled, team);
        }

        /// <summary>
        /// Triggers a named contextual situation on a unit (S2C_NotifyContextualSituation) so clients
        /// with vision play the matching contextual VO/emote/animation. Used for server-timed
        /// situations like "RecallLeadIn" / "RecallWindDown".
        /// </summary>
        public static void NotifyContextualSituation(AttackableUnit unit, string situationName)
        {
            _game.PacketNotifier.NotifyS2C_NotifyContextualSituation(unit, situationName);
        }

        public static void SetSpellToolTipVar<T>(AttackableUnit unit, int tipIndex, T value, SpellbookType book,
            byte slot, SpellSlotType slotType)
            where T : struct
        {
            if (unit is Champion champ)
            {
                slot = (byte)ConvertAPISlot(book, slotType, slot);

                champ.Spells[(short)slot].SetToolTipVar(tipIndex, value);
            }
        }

        public static void SetBuffToolTipVar<T>(Buff buff, int tipIndex, T value)
            where T : struct
        {
            buff.SetToolTipVar(tipIndex, value);
        }

        public static void SpellCast(ObjAIBase caster, int slot, SpellSlotType slotType, Vector2 pos, Vector2 endPos,
            bool fireWithoutCasting, Vector2 overrideCastPos, List<CastTarget> targets = null,
            bool isForceCastingOrChanneling = false, int overrideForceLevel = -1, bool updateAutoAttackTimer = false,
            bool useAutoAttackSpell = false, CastInfo inheritVariablesFrom = null, bool isContinuation = false)
        {
            slot = ConvertAPISlot(slotType, slot);

            Spell spell = caster.Spells[(short)slot];

            if (targets == null)
            {
                targets = new List<CastTarget> { new CastTarget(null, HitResult.HIT_Normal) };
            }

            CastInfo castInfo = new CastInfo()
            {
                SpellHash = (uint)spell.GetId(),
                SpellNetID = _game.NetworkIdManager.GetNewNetId(),
                SpellLevel = spell.CastInfo.SpellLevel,
                AttackSpeedModifier = caster.Stats.AttackSpeedMultiplier.Total,
                Owner = caster,
                // Pet casts report the owning champion (replay-verified — see CastInfo doc).
                SpellChainOwnerNetID = CastInfo.ResolveChainOwnerNetId(caster),
                PackageHash = caster.GetObjHash(),
                MissileNetID = _game.NetworkIdManager.GetNewNetId(),
                TargetPosition = new Vector3(pos.X, caster.GetHeight(), pos.Y),
                TargetPositionEnd = new Vector3(endPos.X, caster.GetHeight(), endPos.Y),

                Targets = targets,

                IsAutoAttack = updateAutoAttackTimer,
                // UseAttackCastTime = attack-pipeline timing (windup + total from the
                // AS-scaled attack cycle); UseAttackCastDelay is derived from the spell's
                // own data flags in Spell.Cast, never forced here — see CastInfo for both.
                UseAttackCastTime = useAutoAttackSpell,
                UseAttackCastDelay = false,
                IsForceCastingOrChannel = isForceCastingOrChanneling,

                SpellSlot = (byte)slot,
                SpellCastLaunchPosition = caster.GetPosition3D()
            };

            if (overrideCastPos != Vector2.Zero)
            {
                castInfo.IsOverrideCastPosition = true;
                // Height at the OVERRIDE position, not at the caster — the launch spot can be
                // on different terrain (e.g. Talon W return blades launch from the blade
                // endpoints; Riot's packet carries the ground height at that spot). Using the
                // caster's height buried remotely-launched missiles under higher terrain.
                castInfo.SpellCastLaunchPosition =
                    new Vector3(overrideCastPos.X, _game.Map.NavigationGrid.GetHeightAtLocation(overrideCastPos), overrideCastPos.Y);

                if (endPos == Vector2.Zero)
                {
                    castInfo.TargetPositionEnd = new Vector3(pos.X, caster.GetHeight(), pos.Y);
                }
            }

            if (overrideForceLevel >= 0)
            {
                castInfo.SpellLevel = (byte)overrideForceLevel;
            }

            // Sub-cast variable inheritance (Riot: one shared LuaVars table per cast chain):
            // the sub-cast's CastInfo SHARES the parent's InstanceVars bag by reference, so every
            // missile/effect spawned by either cast reads and writes the same per-cast state.
            if (inheritVariablesFrom != null)
            {
                castInfo.InstanceVars = inheritVariablesFrom.InstanceVars;
            }

            spell.Cast(castInfo, !fireWithoutCasting, isContinuation);
        }

        public static void SpellCast(ObjAIBase caster, int slot, SpellSlotType slotType, bool fireWithoutCasting,
            AttackableUnit target, Vector2 overrideCastPos, bool isForceCastingOrChanneling = false,
            int overrideForceLevel = -1, bool updateAutoAttackTimer = false, bool useAutoAttackSpell = false,
            CastInfo inheritVariablesFrom = null, bool isContinuation = false)
        {
            CastTarget castTarget = new CastTarget(target,
                CastTarget.GetHitResult(target, useAutoAttackSpell, caster.IsNextAutoCrit, caster.IsNextAutoMiss, caster.IsNextAutoDodged));

            SpellCast(caster, slot, slotType, target.Position, target.Position, fireWithoutCasting, overrideCastPos,
                new List<CastTarget> { castTarget }, isForceCastingOrChanneling, overrideForceLevel,
                updateAutoAttackTimer, useAutoAttackSpell, inheritVariablesFrom, isContinuation);
        }

        public static void SpellCastItem(ObjAIBase caster, string itemSpellName, bool fireWithoutCasting,
            AttackableUnit target, Vector2 overrideCastPos, bool isForceCastingOrChanneling = false,
            int overrideForceLevel = -1, bool updateAutoAttackTimer = false, bool useAutoAttackSpell = false)
        {
            // Apply the spell to the TempItemSlot.
            caster.SetSpell(itemSpellName, (byte)SpellSlotType.TempItemSlot, true);
            SpellCast(caster, 0, SpellSlotType.TempItemSlot, fireWithoutCasting, target, overrideCastPos,
                isForceCastingOrChanneling, overrideForceLevel, updateAutoAttackTimer, useAutoAttackSpell);
        }

        public static void SpellCastItem(ObjAIBase caster, string itemSpellName, Vector2 pos, Vector2 endPos,
            bool fireWithoutCasting, Vector2 overrideCastPos, bool isForceCastingOrChanneling = false,
            int overrideForceLevel = -1, bool updateAutoAttackTimer = false, bool useAutoAttackSpell = false)
        {
            // Apply the spell to the TempItemSlot.
            caster.SetSpell(itemSpellName, (byte)SpellSlotType.TempItemSlot, true);
            SpellCast(caster, 0, SpellSlotType.TempItemSlot, pos, endPos, fireWithoutCasting, overrideCastPos, null,
                isForceCastingOrChanneling, overrideForceLevel, updateAutoAttackTimer, useAutoAttackSpell);
        }

        public static void StopChanneling(ObjAIBase target, ChannelingStopCondition stopCondition,
            ChannelingStopSource stopSource)
        {
            target.StopChanneling(stopCondition, stopSource);
        }

        /// <summary>
        /// Creates a DeathData variable for use with the AttackableUnit.Die() function.
        /// </summary>
        /// <param name="zombify">Whether or not the unit should become a zombie after death.</param>
        /// <param name="deathType">Type of death. Dead wire field (client ignores it) — pass
        /// DieType.MINION_DIE (0), matching Riot's unconditional server value.</param>
        /// <param name="unit">Unit that died.</param>
        /// <param name="killer">Killer of the unit.</param>
        /// <param name="dmgType">Type of damage that caused the death.</param>
        /// <param name="dmgSource">Source of the damage that caused the death.</param>
        /// <param name="duration">Time until the death completes (fade-out?).</param>
        /// <returns></returns>
        public static DeathData CreateDeathData(bool zombify, DieType deathType, AttackableUnit unit,
            AttackableUnit killer, DamageType dmgType, DamageSource dmgSource, float duration)
        {
            return new DeathData
            {
                BecomeZombie = zombify,
                DieType = deathType,
                Unit = unit,
                Killer = killer,
                DamageType = dmgType,
                DamageSource = dmgSource,
                DeathDuration = duration
            };
        }

        /// <summary>
        /// Returns whether or not the designed team has vision over an unit or not
        /// </summary>
        /// <param name="team"></param>
        /// <param name="unit"></param>
        /// <returns></returns>
        public static bool TeamHasVision(TeamId team, GameObject unit)
        {
            return _game.ObjectManager.TeamHasVisionOn(team, unit);
        }

        /// <summary>
        /// Gets a list of waypoints, which forms a path to the desired destination
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="distanceThreshold"></param>
        /// <returns></returns>
        public static List<Vector2> GetPath(Vector2 from, Vector2 to, float distanceThreshold = 0)
        {
            return _game.Map.PathingHandler.GetPath(from, to, distanceThreshold)?.ToList();
        }

        /// <summary>
        /// Runs <paramref name="pos"/> through the lane-minion stand-cell reservation handshake
        /// (<see cref="LeagueSandbox.GameServer.Handlers.PathingHandler.CommitStand"/>): if another
        /// lane minion has already committed a stand within body distance, the position is relocated
        /// to the nearest get-to-able unreserved cell, and the result is reserved for
        /// <paramref name="attacker"/>. Pass-through for non-lane-minions. Use for SHARED approach
        /// targets (e.g. the turret-cap point) so simultaneous walkers fan onto distinct cells
        /// instead of fusing on one coordinate (tt120: capped casters all move-targeted the exact
        /// turret center, converged during the walk and ended stacked on one attack position).
        /// </summary>
        public static Vector2 CommitAttackStandPosition(AttackableUnit attacker, Vector2 pos)
        {
            return _game.Map.PathingHandler.CommitStand(attacker, pos);
        }

        /// <summary>
        /// ACTOR-AWARE path for <paramref name="unit"/>: routes around other units that would block it
        /// (the same A* the engine's attack-approach uses), unlike the terrain-only
        /// <see cref="GetPath(Vector2,Vector2,float)"/> overload which walks straight through bodies.
        /// <paramref name="skipLineOfSight"/> defaults true so the A* runs even when terrain-LOS is
        /// clear — that is what makes a lane minion ROUTE AROUND an allied wave (Riot keeps ~33u
        /// clearance, wire-measured) instead of issuing a straight line through it. Use for lane
        /// forward-push / any "walk past bodies" movement.
        /// </summary>
        public static List<Vector2> GetPath(AttackableUnit unit, Vector2 to, bool skipLineOfSight = true,
            float ignoreTargetRadius = -1f)
        {
            if (unit == null)
            {
                return null;
            }
            return _game.Map.PathingHandler.GetPath(unit, to, skipLineOfSight: skipLineOfSight,
                ignoreTargetRadius: ignoreTargetRadius)?.ToList();
        }

        /// <summary>
        /// Cheap local window reachability check (A2 — S1:9069 / S4:1694). Returns true if
        /// <paramref name="to"/> is reachable from <paramref name="unit"/>'s current position
        /// within about ~4 cells, considering both static terrain and actor blocking. Use as a
        /// pre engagement check in AI scripts before committing to a full <see cref="GetPath"/> —
        /// e.g., "is this melee target reachable right now?". Returns false if path is blocked
        /// inside the local window. For long-range reachability use <see cref="GetPath"/> and
        /// check for null/partial result.
        /// </summary>
        public static bool CheckIsGetToAble(AttackableUnit unit, Vector2 to,
            float radius = 0, float ignoreTargetRadius = 0)
        {
            return _game.Map.PathingHandler.CheckIsGetToAble(unit, to, radius, ignoreTargetRadius);
        }

        /// <summary>
        /// Spiral-snap helper (A2 — S1:9686 / S4:12581). Returns the requested
        /// <paramref name="target"/> if <paramref name="unit"/> can locally reach it, otherwise
        /// spirals outward from <paramref name="target"/> and returns the nearest cell that's
        /// locally reachable from the unit's position. Useful for "snap movement target to a
        /// reachable cell" when the user/AI requested an occluded destination — actor-aware,
        /// unlike server's plain <c>GetClosestTerrainExit</c> which only considers static terrain.
        /// Falls back to the original <paramref name="target"/> if no reachable cell is found
        /// within the spiral cap.
        /// </summary>
        public static Vector2 SetToNearestGetToAbleCell(AttackableUnit unit, Vector2 target,
            float radius = 0, float ignoreTargetRadius = 0, float targetRadius = 0)
        {
            return _game.Map.PathingHandler.SetToNearestGetToAbleCell(unit, target,
                radius, ignoreTargetRadius, targetRadius);
        }

        public static void OverrideUnitAttackSpeedCap(AttackableUnit unit, bool doOverrideMax,
            float maxAttackSpeedOverride, bool doOverrideMin, float minAttackSpeedOverride)
        {
            // Store server-side so the windup/cycle timing (SpellData.GetCharacterAttackDelay) applies the
            // same cap the client is told about — otherwise the server would use the global cap and diverge.
            unit.MaxAttackSpeedOverride = doOverrideMax ? maxAttackSpeedOverride : 0f;
            unit.MinAttackSpeedOverride = doOverrideMin ? minAttackSpeedOverride : 0f;
            _game.PacketNotifier.NotifyS2C_UpdateAttackSpeedCapOverrides(doOverrideMax, maxAttackSpeedOverride,
                doOverrideMin, minAttackSpeedOverride, unit);
        }

        public static ItemData GetItemData(int Id)
        {
            return _game.ItemManager.SafeGetItemType(Id);
        }

        public static void PlaySound(string soundName, AttackableUnit soundOwner)
        {
            _game.PacketNotifier.NotifyS2C_PlaySound(soundName, soundOwner);
        }

        public static void StopTargetingUnit(AttackableUnit unit)
        {
            _game.ObjectManager.StopTargeting(unit);
        }

        public static Pet CreatePet
        (
            Champion owner,
            Spell spell,
            Vector2 position,
            string name,
            string model,
            string buffName,
            float lifeTime,
            bool cloneInventory = true,
            bool showMinimapIfClone = true,
            bool disallowPlayerControl = false,
            bool doFade = false,
            bool isClone = true,
            Stats stats = null,
            string aiScript = "Pet"
        )
        {
            return new Pet(_game, owner, spell, position, name, model, buffName, lifeTime, stats, cloneInventory,
                showMinimapIfClone, disallowPlayerControl, doFade, isClone, aiScript);
        }

        public static Pet CreateClonePet
        (
            Champion owner,
            Spell spell,
            ObjAIBase cloned,
            Vector2 position,
            string buffName,
            float lifeTime,
            bool cloneInventory = true,
            bool showMinimapIfClone = true,
            bool disallowPlayerControl = false,
            bool doFade = false,
            Stats stats = null,
            string AIScript = "Pet"
        )
        {
            return new Pet(_game, owner, spell, cloned, position, buffName, lifeTime, stats, cloneInventory,
                showMinimapIfClone, disallowPlayerControl, doFade, AIScript);
        }

        public static float GetPetReturnRadius(Minion minion = null)
        {
            if (minion != null && minion is Pet pet)
            {
                return pet.GetReturnRadius();
            }

            return GlobalData.ObjAIBaseVariables.DefaultPetReturnRadius;
        }

        /// <summary>
        /// Sets the distance past which a pet starts following its owner back (Riot's per-pet pet-return
        /// radius — the threshold the pet AI's leash/follow uses). Not present in spell/stats data, so a
        /// summon script sets it per pet (e.g. Annie's R for Tibbers); defaults to
        /// <see cref="ObjAIBaseVariables.DefaultPetReturnRadius"/> (200) when unset.
        /// </summary>
        public static void SetPetReturnRadius(Minion minion, float radius)
        {
            if (minion is Pet pet)
            {
                pet.SetReturnRadius(radius);
            }
        }

        /// <summary>
        /// Add a marker for assist to the target.
        /// </summary>
        /// <param name="target">Assist target.</param>
        /// <param name="source">Unit credited for assist.</param>
        /// <param name="duration">Assist duration in seconds.</param>
        public static void ApplyAssistMarker(AttackableUnit target, AttackableUnit source, float duration)
        {
            if (target == null || source is not ObjAIBase objSource)
            {
                _logger.Warn("Can't ApplyAssistMarker! (Target is null or Source is not ObjAIBase!)");
                return;
            }

            target.AddAssistMarker(objSource, duration);
        }

        /// <summary>
        /// Emits the full 4-packet Voracity wire-pattern verified empirically against a
        /// Kat-perspective replay (22 events × 4 packets, 100% consistent shape):
        ///
        /// <para>Per Voracity event (kill OR assist):
        /// <list type="bullet">
        /// <item>3× <c>CHAR_SetCooldown(slot=0/1/2, isSummonerSpell=false, cd=0, max=-1)</c> —
        ///       per-target to Katarina's own client (Q/W/E reset to 0; allies don't render her
        ///       ability cooldowns so broadcast scope would be wasted).</item>
        /// <item>1× <c>CHAR_SetCooldown(slot=3, cd=R_remaining, max=-1)</c> with
        ///       <c>isSummonerSpell</c> scope-toggled by event type:
        ///       <list type="bullet">
        ///       <item>KILL → <c>isSummonerSpell=true</c>, BROADCAST to all teammates with vision
        ///             (carries the Voracity icon-refresh visual + Kat's R cooldown).</item>
        ///       <item>ASSIST → <c>isSummonerSpell=false</c>, per-target to Kat only (just R
        ///             cooldown; no broadcast visual since allies got their own kill credit).</item>
        ///       </list></item>
        /// </list></para>
        ///
        /// <para>Q/W/E are RESET (cd=0), not "subtract 15s" — Riot's server effectively does
        /// `LowerCooldown(15) → floor at 0`, but Q/W/E base CDs are all ≤ 15s in 4.x so the floor
        /// always engages. Wire just sends 0. R uses the formula
        /// <c>R_last_cast_cd − Δt − 15×takedowns</c>; can go negative (signed-float wire field).</para>
        ///
        /// <para>Decomp confirmed no Katarina-specific code in the client; the 4-packet fan-out
        /// happens server-side. The S4 client just receives 4 standard SetCooldown packets and
        /// applies them to its local SpellDataInst[slot] timers.</para>
        /// </summary>
        /// <param name="katarina">Champion that just procced Voracity.</param>
        /// <param name="isOwnKill">true = Katarina's own kill (broadcast slot=3); false = assist (per-target slot=3).</param>
        /// <param name="rRemainingCooldown">Katarina R's effective cooldown post-Voracity (in seconds, can be negative).</param>
        public static void NotifyVoracityProc(ObjAIBase katarina, bool isOwnKill, float rRemainingCooldown)
        {
            if (katarina is not Champion champ) return;
            int ownerUserId = champ.ClientId;

            // Q/W/E per-target reset to 0
            for (byte slot = 0; slot <= 2; slot++)
            {
                _game.PacketNotifier.NotifyCHAR_SetCooldownRaw(katarina,
                    slotId: slot,
                    cooldown: 0f,
                    useAlternateSpellbook: false,
                    maxCooldownForDisplay: -1f,
                    userId: ownerUserId);
            }

            // R: broadcast on kill (Voracity sentinel), per-target on assist
            if (isOwnKill)
            {
                _game.PacketNotifier.NotifyCHAR_SetCooldownRaw(katarina,
                    slotId: 3,
                    cooldown: rRemainingCooldown,
                    useAlternateSpellbook: true,
                    maxCooldownForDisplay: -1f);
            }
            else
            {
                _game.PacketNotifier.NotifyCHAR_SetCooldownRaw(katarina,
                    slotId: 3,
                    cooldown: rRemainingCooldown,
                    useAlternateSpellbook: false,
                    maxCooldownForDisplay: -1f,
                    userId: ownerUserId);
            }
        }

        /// <summary>
        /// Sets the unit stealthed/faded.
        /// </summary>
        public static Fade PushCharacterFade(
            AttackableUnit target,
            float fadeAmount,
            float fadeTime,
            Fade ID = null
        )
        {
            if (ID != null && fadeAmount == 0)
            {
                target.FadeIn(ID, fadeTime);
                return null;
            }

            return target.FadeOut(fadeAmount, fadeTime);
        }

        public static void PopCharacterFade(
            AttackableUnit target,
            Fade ID
        )
        {
            target.FadeIn(ID);
        }

        public static void NotifyDisplayFloatingText(FloatingTextData floatTextData, TeamId team = 0, int userId = -1)
        {
            _game.PacketNotifier.NotifyDisplayFloatingText(floatTextData, team, userId);
        }

        /// <summary>
        /// Makes <paramref name="unit"/> "say" a floating localization-key message (NPC_MessageToClient) —
        /// e.g. spell-block / immune / invulnerable feedback ("game_lua_BlackShield_immune",
        /// "game_floatingtext_invulnerable", "game_lua_UndyingRage"). Unifies both wire variants by audience:
        /// <paramref name="sayTo"/> null → broadcast to everyone (NPC_MessageToClient_Broadcast 0x18);
        /// otherwise only that player's client sees it (NPC_MessageToClient_MapView 0x128). The recipient's
        /// userId is resolved from its owning player, so scripts pass units, not raw ids.
        /// </summary>
        public static void Say(AttackableUnit unit, string message, uint floatTextType = 0, ObjAIBase sayTo = null)
        {
            int userId = -1;
            if (sayTo is Champion champion)
            {
                userId = _game.PlayerManager.GetClientInfoByChampion(champion)?.ClientId ?? -1;
            }
            _game.PacketNotifier.NotifyNPC_MessageToClient(unit, message, floatTextType, userId: userId);
        }

        /// <summary>
        /// Moves the camera of the player controlling the specified unit to a location.
        /// </summary>
        /// <param name="unit">Unit whose owning player's camera should move.</param>
        /// <param name="cameraTimer">Travel time in seconds.</param>
        /// <param name="finalCameraPosition">Destination camera position.</param>
        public static void MoveCamera(ObjAIBase unit, float cameraTimer, Vector3 finalCameraPosition)
        {
            if (unit == null)
            {
                return;
            }

            var player = _game.PlayerManager.GetPlayers(false).FirstOrDefault(p => p?.Champion?.NetId == unit.NetId);
            if (player == null)
            {
                return;
            }

            _game.PacketNotifier.NotifyS2C_MoveCameraToPoint(player, Vector3.Zero, finalCameraPosition, cameraTimer);
        }

        public static void NotifyWaypointGroup(AttackableUnit unit)
        {
            _game.PacketNotifier.NotifyWaypointGroup(unit);
        }

        /// <summary>
        /// Sends a single-waypoint WaypointGroup with HasTeleportID=true on CHL_S2C — used
        /// for blink-style spells (Katarina E) where the post-blink position must reach the
        /// client same-tick to match Riot's wire timing. The unit's TeleportID is read after
        /// this call returns, so increment it (e.g. via TeleportTo) before invoking.
        /// </summary>
        public static void NotifyTeleport(AttackableUnit unit, Vector2 position)
        {
            _game.PacketNotifier.NotifyTeleport(unit, position);
        }

        /// <summary>
        /// Changes a property of the spell in the given slot (icon index, name, range, targeting type and etc.)
        /// on the owning player's client/HUD. The user is resolved from the owner automatically.
        /// </summary>
        public static void ChangeSlotSpellData(ObjAIBase owner, byte slot,
            ChangeSlotSpellDataType changeType, bool isSummonerSpell = false,
            TargetingType targetingType = TargetingType.Invalid, string newName = "", float newRange = 0,
            float newMaxCastRange = 0, float newDisplayRange = 0, byte newIconIndex = 0x0,
            List<uint> offsetTargets = null)
        {
            if (owner is Champion champion)
            {
                _game.PacketNotifier.NotifyChangeSlotSpellData(champion.ClientId, owner, slot, changeType,
                    isSummonerSpell, targetingType, newName, newRange, newMaxCastRange, newDisplayRange,
                    newIconIndex, offsetTargets);
            }
        }

        public static SpellMissile CreateCustomMissile(ObjAIBase caster, int slot, SpellSlotType slotType,
            Vector2 start, Vector2 end, MissileParameters parameters, bool isForceCastingOrChannel = false,
            bool isOverrideCastPosition = true, float? customHeightOffset = null, AttackableUnit target = null)
        {
            slot = ConvertAPISlot(slotType, slot);

            if (slot == -1) return null;

            Spell spell = caster.Spells[(short)slot];
            if (spell == null) return null;

            return spell.CreateCustomMissile(start, end, parameters, isForceCastingOrChannel, isOverrideCastPosition,
                customHeightOffset, target);
        }

        public static SpellMissile CreateCustomMissile(ObjAIBase caster, string spellName, Vector2 start, Vector2 end,
            MissileParameters parameters, bool isForceCastingOrChannel = false, bool isOverrideCastPosition = true,
            float? customHeightOffset = null, AttackableUnit target = null)
        {
            Spell spell = caster.GetSpell(spellName);

            if (spell == null)
            {
                LogDebug($"CreateCustomMissile Error: Could not find spell with name '{spellName}' on {caster.Name}");
                return null;
            }

            return spell.CreateCustomMissile(start, end, parameters, isForceCastingOrChannel, isOverrideCastPosition,
                customHeightOffset, target);
        }

        /// <summary>
        /// Unified charge-fire trigger. Replay-verified Riot patterns — both styles always emit
        /// the parent channel-end signal (<c>NPC_CastSpellAns(parent slot, SAME NetIDs as
        /// charge-start, IsContinuationCast=true)</c>) to clear the client charge bar. The
        /// <paramref name="fireWithoutCasting"/> flag picks which sub-action accompanies it:
        /// <list type="bullet">
        ///   <item><b><c>fireWithoutCasting = true</c></b> (default, Missile-style, Varus Q
        ///   pattern): bypasses sub-spell Cast pipeline, spawns the missile directly via
        ///   <see cref="Spell.CreateReplicatedMissile"/> which broadcasts <c>MissileReplication</c>.
        ///   Sub-spell's OnSpellPreCast/Cast/PostCast hooks DO NOT fire. Use when the sub-spell
        ///   is just a missile (no per-cast setup needed besides on-hit damage).</item>
        ///   <item><b><c>fireWithoutCasting = false</c></b> (Sector/effect-style, Xerath Q
        ///   pattern): runs full <see cref="SpellCast"/> on the sub-spell. Emits
        ///   <c>NPC_CastSpellAns(sub-slot, NEW NetIDs, IsContinuationCast=false)</c> AND triggers the
        ///   sub-script's OnSpellPreCast/Cast/PostCast hooks (sector creation, particles, anim,
        ///   status flags). NO <c>MissileReplication</c>. Use when the sub-spell needs its own
        ///   cast-pipeline logic.</item>
        /// </list>
        /// </summary>
        /// <param name="parameters">Optional, only used when <paramref name="fireWithoutCasting"/>
        /// is true (missile-style). If null, falls back to sub-spell's
        /// <c>ScriptMetadata.MissileParameters</c>.</param>
        /// <returns>The spawned missile when <paramref name="fireWithoutCasting"/> is true,
        /// otherwise null (sector-style doesn't return a missile).</returns>
        public static SpellMissile SpellCastCharge(Spell chargeSpell, int subSlot,
            SpellSlotType subSlotType, Vector2 start, Vector2 targetPos,
            bool fireWithoutCasting = true, MissileParameters parameters = null)
        {
            var caster = chargeSpell.CastInfo.Owner;
            SpellMissile missile = null;

            if (fireWithoutCasting)
            {
                // Missile-style — resolve sub-spell, spawn via CreateReplicatedMissile.
                int resolvedSlot = ConvertAPISlot(subSlotType, subSlot);
                if (resolvedSlot < 0 || !caster.Spells.ContainsKey((short)resolvedSlot))
                {
                    LogDebug($"SpellCastCharge: invalid sub-slot {subSlot} of type {subSlotType} on {caster.Name}");
                }
                else
                {
                    var subSpell = caster.Spells[(short)resolvedSlot];
                    if (subSpell != null)
                    {
                        var effectiveParams = parameters ?? subSpell.Script?.ScriptMetadata?.MissileParameters;
                        if (effectiveParams != null)
                        {
                            missile = subSpell.CreateReplicatedMissile(start, targetPos, effectiveParams,
                                isForceCastingOrChannel: true);
                        }
                        else
                        {
                            LogDebug($"SpellCastCharge: no MissileParameters for missile-style on sub-spell '{subSpell.SpellName}'");
                        }
                    }
                }
            }
            else
            {
                // Sector/effect-style — full SpellCast pipeline. Sub-script's OnSpellPreCast etc.
                // hooks run. Wire emits NPC_CastSpellAns(sub-slot, new NetIDs, IsContinuationCast=false).
                SpellCast(caster, subSlot, subSlotType, start, targetPos,
                    fireWithoutCasting: false, overrideCastPos: Vector2.Zero);
            }

            // Always: parent channel-end signal NPC_CastSpellAns(parent-slot, SAME NetIDs as
            // charge-start, IsContinuationCast=true). Tells the client to exit charge state and clear
            // the charge HUD. Without this packet the client keeps the HUD bar visible
            // indefinitely — empirically verified 2026-05-17 (collapsing into the sub-spell
            // packet via isContinuation didn't fire the parent's exit-charge handler).
            var targetPos3D = new Vector3(targetPos.X, caster.GetHeight(), targetPos.Y);
            chargeSpell.NotifyChargeFireCastSpellAns(targetPos3D);

            return missile;
        }

        public static Vector2 GetClosestTerrainExit(Vector2 location, float distanceThreshold = 0)
        {
            return _game.Map.NavigationGrid.GetClosestTerrainExit(location, distanceThreshold);
        }

        public static void UnitSetLookAt(AttackableUnit attacker, AttackableUnit attacked, AttackType attackType)
        {
            _game.PacketNotifier.NotifyS2C_UnitSetLookAt(attacker, attacked, attackType);
        }

        public static List<SpellMissile> GetMissiles()
        {
            return _game.ObjectManager.GetAllMissiles();
        }

        // === AreaTrigger subsystem (Riot LoL::AreaTriggerManager) — faithful replacement for SpellSector.
        // Server-internal geometric regions, NOT replicated. See docs/AREATRIGGER_REWRITE_PLAN.md. ===

        /// <summary>
        /// Creates a server-side circular trigger region (Riot AreaTriggerSphere). Callbacks fire per tick
        /// for units inside (the script owns all gameplay logic). Returns the trigger id for Update/Delete.
        /// </summary>
        public static int CreateAreaTriggerSphere(Vector2 center, float radius,
            Action<AttackableUnit> onEnter = null, Action<AttackableUnit> onExit = null,
            Action<AttackableUnit> onUpdate = null, Action<SpellMissile> onDestroyMissile = null)
        {
            return _game.AreaTriggerManager.CreateSphere(center, radius, onEnter, onExit, onUpdate, onDestroyMissile);
        }

        /// <summary>
        /// Like <see cref="CreateAreaTriggerSphere"/> but the region's center tracks <paramref name="follow"/>
        /// every tick (Riot AreaTrigger attach-to-unit) — for owner-following zones (Fiddlesticks R Crowstorm).
        /// Caller owns the lifetime (Delete on a timer / when the source ends).
        /// </summary>
        public static int CreateAreaTriggerSphereAttached(LeagueSandbox.GameServer.GameObjects.GameObject follow,
            float radius, Action<AttackableUnit> onEnter = null, Action<AttackableUnit> onExit = null,
            Action<AttackableUnit> onUpdate = null, Action<SpellMissile> onDestroyMissile = null)
        {
            return _game.AreaTriggerManager.CreateSphereAttached(follow, radius, onEnter, onExit, onUpdate, onDestroyMissile);
        }

        /// <summary>
        /// Creates a server-side wall trigger region (Riot AreaTriggerWall — Windwall). The segment
        /// [p1,p2] spans the wall width; <paramref name="thickness"/> is the catch band along travel.
        /// When <paramref name="destroysMissiles"/>, crossing enemy missiles are destroyed centrally by the
        /// missile path. Returns the trigger id.
        /// </summary>
        public static int CreateAreaTriggerWall(Vector2 p1, Vector2 p2, float thickness, bool destroysMissiles,
            TeamId wallTeam, Action<AttackableUnit> onEnter = null, Action<AttackableUnit> onExit = null,
            Action<AttackableUnit> onUpdate = null, Action<SpellMissile> onDestroyMissile = null)
        {
            return _game.AreaTriggerManager.CreateWall(p1, p2, thickness, destroysMissiles, wallTeam,
                onEnter, onExit, onUpdate, onDestroyMissile);
        }

        /// <summary>Moves a wall trigger's endpoints (steered per tick by a moving-wall script).</summary>
        public static void UpdateAreaTriggerWallEndpoints(int id, Vector2 p1, Vector2 p2)
        {
            if (_game.AreaTriggerManager.Find(id) is AreaTriggerWall wall)
            {
                wall.P1 = p1;
                wall.P2 = p2;
            }
        }

        /// <summary>Removes a trigger region (Riot AreaTriggerManager::Delete). No-op on unknown id.</summary>
        public static void DeleteAreaTrigger(int id)
        {
            _game.AreaTriggerManager.Delete(id);
        }

        public static void InstantStopTest(AttackableUnit unit, bool forceClient = true, bool keepAnimating = false)
        {
            _game.PacketNotifier.NotifyNPC_InstantStop_Attack(unit, false, keepAnimating, false, true, forceClient, 0);
        }

        /// <summary>
        /// Broadcasts NPC_InstantStop_Attack with the replay-verified all-flags-false wire shape
        /// (100% of 32,808 packets). Client-side this force-finalizes the active spellcasting
        /// instance (S4 obj_AI_Base::DoInstantStopAttack -> StopSpellcastingObjectForced/ForceStop)
        /// — the ONLY path that clears a CantCancelWhileWindingUp=1 cast frame. Riot sends this at
        /// windup end for casts like Nami Q (78/93 casts, median +255ms = the FinishCasting tick);
        /// without it the client's cast frame sticks and silently eats the next spell press.
        /// </summary>
        public static void NotifyInstantStopAttack(AttackableUnit unit, bool isSummonerSpell = false, Spell spell = null)
        {
            // Pass the stopping spell so the ISA carries its reserved CastInfo.MissileNetID
            // (Riot threads castInfo.missileNetworkID through every ISA; inert with the
            // flags-false shape, but wire-faithful).
            _game.PacketNotifier.NotifyNPC_InstantStop_Attack(unit, isSummonerSpell,
                missileNetID: spell?.CastInfo.MissileNetID ?? 0);
        }

        /// <summary>
        /// Fades in a fullscreen color tint (port of Riot's <c>BBFadeInColorFadeEffect</c>:
        /// ColorRed/Green/Blue + FadeTime + MaxWeight + SpecificToTeam — S2C_ColorRemapFX on the
        /// wire, client Gamma::ColorOverride). <paramref name="maxWeight"/> is the tint's target
        /// blend strength 0..1, independent of alpha (which Riot fixes at 255). Team-scoped
        /// broadcast form — the NocturneParanoia.lua shape: blue (0,0,75) @ 0.3 to Nocturne's
        /// team + red (75,0,0) @ 0.3 to the enemy team, fade 1.0. TEAM_UNKNOWN = every player.
        /// For the per-player unicast form (stealth self-tint) use the Champion overload.
        /// </summary>
        /// <param name="colorRed">Red component of the tint (0-255).</param>
        /// <param name="colorGreen">Green component of the tint (0-255).</param>
        /// <param name="colorBlue">Blue component of the tint (0-255).</param>
        /// <param name="fadeTime">Seconds to ramp the tint in.</param>
        /// <param name="maxWeight">Target blend strength 0..1.</param>
        /// <param name="specificToTeam">Team whose players apply the tint (TEAM_UNKNOWN = all).</param>
        public static void FadeInColorFadeEffect(byte colorRed, byte colorGreen, byte colorBlue,
            float fadeTime, float maxWeight, TeamId specificToTeam = TeamId.TEAM_UNKNOWN)
        {
            var color = new GameServerCore.Content.Color
            {
                R = colorRed, G = colorGreen, B = colorBlue, A = 255
            };
            _game.PacketNotifier.NotifyTint(specificToTeam, true, fadeTime, color, maxWeight);
        }

        /// <summary>
        /// Unicast variant of <see cref="FadeInColorFadeEffect(byte,byte,byte,float,float,TeamId)"/>:
        /// tints only the screen of the player controlling <paramref name="unit"/> — the stealth
        /// self-tint form (replay: dark blue (0,0,50) @ 0.2 for TalonR/AkaliW/KhazixR/ShacoQ,
        /// sent only to the stealther). Takes any unit like Riot's BBs (scripts pass "Owner");
        /// the hero check lives HERE, engine-side (Riot's TypeInfo::objIsHero pattern) — a
        /// non-champion unit has no client to tint, so this is a no-op for pets/clones/boxes.
        /// </summary>
        public static void FadeInColorFadeEffect(AttackableUnit unit, byte colorRed, byte colorGreen,
            byte colorBlue, float fadeTime, float maxWeight)
        {
            if (unit is not Champion champion)
            {
                return;
            }
            var color = new LeaguePackets.Game.Common.Color
            {
                Red = colorRed, Green = colorGreen, Blue = colorBlue, Alpha = 255
            };
            _game.PacketNotifier.ColorRemapFx(champion, true, fadeTime, color, maxWeight, false);
        }

        /// <summary>
        /// Fades a previously applied fullscreen tint back out (port of Riot's
        /// <c>BBFadeOutColorFadeEffect</c>: FadeTime + SpecificToTeam).
        /// </summary>
        /// <param name="fadeTime">Seconds to ramp the tint out.</param>
        /// <param name="specificToTeam">Team whose players clear the tint (TEAM_UNKNOWN = all).</param>
        public static void FadeOutColorFadeEffect(float fadeTime, TeamId specificToTeam = TeamId.TEAM_UNKNOWN)
        {
            _game.PacketNotifier.NotifyTint(specificToTeam, false, fadeTime,
                new GameServerCore.Content.Color { R = 0, G = 0, B = 0, A = 0 }, 0f);
        }

        /// <summary>
        /// Unicast variant of <see cref="FadeOutColorFadeEffect(float,TeamId)"/> for the screen
        /// of the player controlling <paramref name="unit"/> (stealth-end form). No-op for
        /// non-champion units, mirroring the fade-in overload.
        /// </summary>
        public static void FadeOutColorFadeEffect(AttackableUnit unit, float fadeTime)
        {
            if (unit is not Champion champion)
            {
                return;
            }
            _game.PacketNotifier.ColorRemapFx(champion, false, fadeTime,
                new LeaguePackets.Game.Common.Color { Red = 0, Green = 0, Blue = 0, Alpha = 0 }, 0f, false);
        }

        /// <summary>
        /// Gets the fountain position for the specified team.
        /// This is the position where champions respawn and buy items.
        /// </summary>
        /// <param name="team">Team to get fountain position for.</param>
        /// <returns>Vector2 position of the fountain for the specified team.</returns>
        public static Vector2 GetFountainPosition(TeamId team)
        {
            return _game.Map.MapScript.GetFountainPosition(team);
        }
        
        public static bool IsInFront(AttackableUnit self, AttackableUnit target)
        {
            return Vector2.Dot(self.Direction.ToVector2(), target.Position - self.Position) > 0;
        }

        public static bool IsBehind(AttackableUnit self, AttackableUnit target)
        {
            return !IsInFront(self, target);
        }

        public static bool AreEmpoweredSumsEnabled()
        {
            return _game.Config.GameFeatures.HasFlag(FeatureFlags.EnableEmpoweredSumsForTesting);
        }
    }
}
