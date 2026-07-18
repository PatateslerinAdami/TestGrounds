using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using log4net;
using System;
using System.Collections.Generic;
using GameServerCore.Packets.Enums;

/*
 * Possible Events:
 * Events that are always performed are accounted for in-script, no event handling needed. EX: Spell calls spellscript.OnActivate()
[OnActivate] - buffs and spells (always performed)
[OnAddPAR]
[OnAllowAddBuff]
[OnAssist]
[OnAssistUnit]
[OnBeingDodged]
[OnBeingHit]
[OnBeingSpellHit]
[OnCanCast]
[OnCollision]
[OnCollisionTerrain]
[OnDeactivate] - buffs and spells (always performed)
[OnDealDamage]
[OnDeath]
[OnDisconnect]
[OnDodge]
[OnEmote]
[OnHeal]
[OnHitUnit]
[OnKill]
[OnMinionKill]
[OnKillUnit]
[OnLaunchAttack]
[OnLaunchMissile]
[OnLevelUp]
[OnLevelUpSpell]
[OnMiss]
[OnMissileEnd]
[OnMissileUpdate]
[OnMoveBegin]
[OnMoveEnd]
[OnMoveFailure]
[OnMoveSuccess]
[OnNearbyDeath]
[OnPathToTargetBlocked]
[OnCancelAttack] - in-progress auto-attack windup cancelled; carries AutoAttackStopReason. (wired)
[OnPreAttack]
[OnPreDamage] - raw pre-mitigation damage; scripts may modify DamageData.Damage. (wired)
[OnPreDealDamage]
[OnPreTakeDamage]
(OnPreMitigationDamage was an S1-only hook; in 4.20 it no longer exists — merged into OnPreDamage.)
[OnReconnect]
[OnResurrect]
[OnSpellCast] - start casting
[OnSpellCooldownEnd] - spell's cooldown finished, back to STATE_READY (natural expiry + manual reset). (wired)
[OnSpellChannel] - start channeling
[OnSpellChannelCancel] - abrupt stop channeling
[OnSpellChannelUpdate] - every server tick during channel; diff=0f at channel entry. Scripts pace via PeriodicTicker.
[OnSpellPostCast] - finish casting
[OnSpellPostChannel] - finish channeling
[OnSpellPreCast] - setup cast info before casting (always performed)
[OnSpellHit] - "ApplyEffects" function in Spell.
[OnTakeDamage]
[OnUpdateActions] - move order probably
[OnUpdateAmmo] - spell ammo/charge count changed (recharge / restore / cast-consume); carries (owner, spell). (wired)
[OnUpdateStats]
[OnZombie] - death produced a zombie (BecomeZombie); unit stays in world until EndZombie(). (wired)
 */

namespace LeagueSandbox.GameServer.API
{
    public static class ApiEventManager
    {
        private static Game _game;
        private static ILog _logger = LoggerProvider.GetLogger();
        private static List<DispatcherBase> _dispatchers = new List<DispatcherBase>();

        internal static void SetGame(Game game)
        {
            _game = game;
        }

        public static void RemoveAllListenersForOwner(object owner)
        {
            foreach (var dispatcher in _dispatchers)
            {
                dispatcher.RemoveListener(owner);
            }
        }

        // Fires when primary ability resource (mana/energy/etc.) is added; published from AttackableUnit.
        public static Dispatcher<AttackableUnit, AttackableUnit> OnAddPAR
            = new Dispatcher<AttackableUnit, AttackableUnit>();

        public static ConditionDispatcher<AttackableUnit, AttackableUnit, Buff> OnAllowAddBuff
            = new ConditionDispatcher<AttackableUnit, AttackableUnit, Buff>();

        public static Dispatcher<AttackableUnit, AttackableUnit> OnBeingHit
            = new Dispatcher<AttackableUnit, AttackableUnit>();

        public static Dispatcher<AttackableUnit, Spell, SpellMissile> OnBeingSpellHit
            = new Dispatcher<AttackableUnit, Spell, SpellMissile>();

        public static Dispatcher<Buff> OnBuffDeactivated
            = new Dispatcher<Buff>();

        public static ConditionDispatcher<AttackableUnit, Spell> OnCanCast
            = new ConditionDispatcher<AttackableUnit, Spell>();

        public static Dispatcher<GameObject, GameObject> OnCollision
            = new Dispatcher<GameObject, GameObject>();

        public static Dispatcher<GameObject> OnCollisionTerrain
            = new Dispatcher<GameObject>();

        public static Dispatcher<ObjAIBase, DeathData> OnAssist
            = new Dispatcher<ObjAIBase, DeathData>();

        public static DataOnlyDispatcher<AttackableUnit, DamageData> OnDealDamage
            = new DataOnlyDispatcher<AttackableUnit, DamageData>();

        public static DataOnlyDispatcher<AttackableUnit, DeathData> OnDeath
            = new DataOnlyDispatcher<AttackableUnit, DeathData>();

        // Fires when a death produces a ZOMBIE rather than a normal death (DeathData.BecomeZombie
        // set during the OnDeath pass). Faithful to Riot's BuffOnZombieBuildingBlocks (decomp:
        // obj_AI_Base::DoDeath sets bZombie=true → buff OnZombie hooks). The zombie unit stays in
        // the world (not removed) and acts until a script calls EndZombie() — e.g. Karthus
        // DeathDefied: OnDeath arms BecomeZombie, OnZombie grants the 7s keep-casting buff.
        public static Dispatcher<AttackableUnit, DeathData> OnZombie
            = new Dispatcher<AttackableUnit, DeathData>();

        public static DataOnlyDispatcher<ObjAIBase, DamageData> OnHitUnit
            = new DataOnlyDispatcher<ObjAIBase, DamageData>();

        public static DataOnlyDispatcher<Champion, ScoreData> OnIncrementChampionScore
            = new DataOnlyDispatcher<Champion, ScoreData>();

        public static DataOnlyDispatcher<AttackableUnit, DeathData> OnKill
            = new DataOnlyDispatcher<AttackableUnit, DeathData>();

        public static DataOnlyDispatcher<AttackableUnit, DeathData> OnMinionKill
            = new DataOnlyDispatcher<AttackableUnit, DeathData>();

        public static DataOnlyDispatcher<AttackableUnit, DeathData> OnKillUnit
            = new DataOnlyDispatcher<AttackableUnit, DeathData>();

        public static DataOnlyDispatcher<ObjAIBase, Spell> OnLaunchAttack
            = new DataOnlyDispatcher<ObjAIBase, Spell>();
        
        //Attack debuffs
        public static Dispatcher<AttackableUnit, AttackableUnit> OnDodge    
            = new Dispatcher<AttackableUnit, AttackableUnit>();
        
        public static Dispatcher<AttackableUnit, AttackableUnit> OnMiss        
            = new Dispatcher<AttackableUnit, AttackableUnit>();
        
        public static Dispatcher<AttackableUnit, AttackableUnit> OnBeingDodged 
            = new Dispatcher<AttackableUnit, AttackableUnit>();

        /// <summary>
        /// Called immediately after the rocket is added to the scene. *NOTE*: At the time of the call, the rocket has not yet been spawned for players.
        /// <summary>
        public static Dispatcher<Spell, SpellMissile> OnLaunchMissile
            = new Dispatcher<Spell, SpellMissile>();

        public static Dispatcher<AttackableUnit> OnLevelUp
            = new Dispatcher<AttackableUnit>();

        public static Dispatcher<Spell> OnLevelUpSpell
            = new Dispatcher<Spell>();

        /// <summary>
        /// Fired when a unit ENTERS forced movement (dash / leap / engine knock-arc) — symmetric with
        /// <see cref="OnMoveEnd"/>. Lets components react to the transition (e.g. drop a CC-wander when a
        /// knockup interrupts) instead of polling <c>MovementParameters != null</c> every tick.
        /// Part of the forced-movement rewrite P1 (see docs/FORCED_MOVEMENT_REWRITE_PLAN.md).
        /// </summary>
        public static Dispatcher<AttackableUnit, ForceMovementParameters> OnMoveBegin
            = new Dispatcher<AttackableUnit, ForceMovementParameters>();

        public static Dispatcher<AttackableUnit, ForceMovementParameters> OnMoveEnd
            = new Dispatcher<AttackableUnit, ForceMovementParameters>();

        public static Dispatcher<AttackableUnit, ForceMovementParameters> OnMoveFailure
            = new Dispatcher<AttackableUnit, ForceMovementParameters>();

        public static Dispatcher<AttackableUnit, ForceMovementParameters> OnMoveSuccess
            = new Dispatcher<AttackableUnit, ForceMovementParameters>();

        /// <summary>
        /// Fired when an ObjAIBase's navigation to its current attack target fails — the goal is
        /// unreachable so the pathfinder returned no path (or only a partial path to the closest
        /// reachable cell). Mirrors the engine-fired Lua callback `OnPathToTargetBlocked`
        /// (Aggro.lua / Shared/Minions.lua / BaronMinionAI.lua): the AI briefly ignores the target
        /// and re-acquires. Published deferred (top of the next Update tick) to avoid re-entrant
        /// target/waypoint mutation inside the pathing pass. Data = the blocked target (may be null).
        /// </summary>
        public static DataOnlyDispatcher<ObjAIBase, AttackableUnit> OnPathToTargetBlocked
            = new DataOnlyDispatcher<ObjAIBase, AttackableUnit>();

        public static DataOnlyDispatcher<ObjAIBase, Spell> OnPreAttack
            = new DataOnlyDispatcher<ObjAIBase, Spell>();

        /// <summary>
        /// Riot OnCancelAttack: fires on the attacking unit when an in-progress auto-attack windup is
        /// cancelled, carrying the <see cref="AutoAttackStopReason"/> (Moving / TargetLost / OtherImmediately / …).
        /// Decomp: BuffScriptInstance::HandleOnCancelAttack(obj_AI_Base*, AutoAttackStopReason).
        /// </summary>
        public static DataOnlyDispatcher<ObjAIBase, AutoAttackStopReason> OnCancelAttack
            = new DataOnlyDispatcher<ObjAIBase, AutoAttackStopReason>();

        /// <summary>
        /// Riot 4.20 OnPreDamage: fires on the RAW (pre-mitigation) damage, before Armor/MR is applied.
        /// Scripts may modify <c>DamageData.Damage</c> here and the engine mitigates the modified value.
        /// Published for both attacker- and target-side buffs. (Riot also priority-orders this via
        /// OnPreDamagePriority — not yet wired; see docs/DAMAGE_PIPELINE_FAITHFUL_PLAN.md P3.)
        /// </summary>
        public static DataOnlyDispatcher<AttackableUnit, DamageData> OnPreDamage
            = new DataOnlyDispatcher<AttackableUnit, DamageData>();

        public static DataOnlyDispatcher<AttackableUnit, DamageData> OnPreDealDamage
            = new DataOnlyDispatcher<AttackableUnit, DamageData>();

        public static DataOnlyDispatcher<AttackableUnit, DamageData> OnPreTakeDamage
            = new DataOnlyDispatcher<AttackableUnit, DamageData>();

        public static Dispatcher<ObjAIBase> OnResurrect
            = new Dispatcher<ObjAIBase>();

        public static Dispatcher<Spell> OnSpellCast
            = new Dispatcher<Spell>();

        public static Dispatcher<Spell> OnSpellChannel
            = new Dispatcher<Spell>();

        public static Dispatcher<Spell, ChannelingStopSource> OnSpellChannelCancel
            = new Dispatcher<Spell, ChannelingStopSource>();

        public static Dispatcher<Spell, float> OnSpellChannelUpdate
            = new Dispatcher<Spell, float>();

        public static Dispatcher<Spell, AttackableUnit, SpellMissile> OnSpellHit
            = new Dispatcher<Spell, AttackableUnit, SpellMissile>();

        public static Dispatcher<SpellMissile> OnSpellMissileEnd
            = new Dispatcher<SpellMissile>();

        // Fires ONCE when a chained missile's whole chain ends — the final hop that spawns no
        // successor (bounce budget reached / no next target / fizzle / cancel / lifetime). Keyed by the
        // ORIGIN spell (constant across a non-alternating chain), carries the final missile. Use this
        // instead of per-hop OnSpellMissileEnd for "after the last bounce" logic (e.g. Master Yi Q
        // reappear). NOTE: for alternating ally/enemy chains (Nami W) the origin switches per segment,
        // so the key is the LAST segment's spell.
        public static Dispatcher<Spell, SpellMissile> OnSpellChainMissileEnd
            = new Dispatcher<Spell, SpellMissile>();

        public static Dispatcher<SpellMissile, AttackableUnit> OnSpellMissileHit
            = new Dispatcher<SpellMissile, AttackableUnit>();

        public static Dispatcher<SpellMissile, float> OnSpellMissileUpdate
            = new Dispatcher<SpellMissile, float>();

        public static Dispatcher<Spell> OnSpellPostCast
            = new Dispatcher<Spell>();

        // Fires when a spell's cooldown finishes and it returns to STATE_READY — both the natural
        // per-tick expiry (Update) and manual resets to 0 (CDR procs / refunds via SetCooldown).
        // Keyed by the Spell. Use for "off-cooldown" triggers (e.g. Master Yi E refresh logic).
        public static Dispatcher<Spell> OnSpellCooldownEnd
            = new Dispatcher<Spell>();

        public static Dispatcher<Spell> OnSpellPostChannel
            = new Dispatcher<Spell>();

        // CHARGE pipeline (UseChargeChanneling=1 spells like Varus Q). Parallel to OnSpellChannel/
        // OnSpellChannelCancel/OnSpellChannelUpdate/OnSpellPostChannel but fires INSTEAD of those for
        // charge-style spells. Engine routes via SpellData.UseChargeChanneling check.
        // - OnSpellChargeStart: charge begins (analogous to OnSpellChannel)
        // - OnSpellChargeTick: per-server-tick during charge (analogous to OnSpellChannelUpdate; diff in ms)
        // - OnSpellChargeFire: release/timeout → missile fire (analogous to OnSpellPostChannel)
        // - OnSpellChargeCancel: real interrupt (stun/silence/death/casting) (analogous to OnSpellChannelCancel)
        // OnSpellChargeUpdate (client cursor update via C2S_SpellChargeUpdateReq) stays in ISpellScript
        // directly because it's driven by an inbound packet, not by the channel lifecycle.
        public static Dispatcher<Spell> OnSpellChargeStart
            = new Dispatcher<Spell>();

        public static Dispatcher<Spell, float> OnSpellChargeTick
            = new Dispatcher<Spell, float>();

        public static Dispatcher<Spell> OnSpellChargeFire
            = new Dispatcher<Spell>();

        public static Dispatcher<Spell, ChannelingStopSource> OnSpellChargeCancel
            = new Dispatcher<Spell, ChannelingStopSource>();

        public static DataOnlyDispatcher<AttackableUnit, DamageData> OnTakeDamage
            = new DataOnlyDispatcher<AttackableUnit, DamageData>();

        public static readonly DataOnlyDispatcher<AttackableUnit, HealData> OnHeal 
            = new DataOnlyDispatcher<AttackableUnit, HealData>();
        
        public static readonly DataOnlyDispatcher<AttackableUnit, HealData> OnCastHeal 
            = new DataOnlyDispatcher<AttackableUnit, HealData>();

        // Riot OnTargetLost(reason, unit): callback gets (owner, lostUnit, reason). LostVisibility drives
        // the champion go-to-last-known re-acquisition (docs/LOST_TARGET_REACQUISITION_PLAN.md).
        public static Dispatcher<ObjAIBase, AttackableUnit, TargetLostReason> OnTargetLost
            = new Dispatcher<ObjAIBase, AttackableUnit, TargetLostReason>();

        public static Dispatcher<ObjAIBase, Emotions> OnEmote
            = new Dispatcher<ObjAIBase, Emotions>();

        // Mirrors the Riot buff-script OnBuffAdded hook: a buff on a unit observing OTHER buffs
        // being activated on that same unit. Primary consumer: spell-shield buffs (BuffType.SPELL_SHIELD,
        // e.g. SivirE) consuming incoming "SpellShieldMarker"/"*SpellShieldCheck" break-attempt buffs
        // (BuildingBlocksBase.lua BBBreakSpellShields; see project_spell_shield_system memory).
        // Keyed by the receiving unit, carrying the newly activated buff. Published from Buff.ActivateBuff,
        // i.e. only for genuinely new instances (new-add / REPLACE_EXISTING) — not for RENEW/STACK refreshes.
        public static Dispatcher<AttackableUnit, Buff> OnUnitBuffActivated
            = new Dispatcher<AttackableUnit, Buff>();

        public static Dispatcher<AttackableUnit, Buff> OnUnitBuffDeactivated
            = new Dispatcher<AttackableUnit, Buff>();

        // Fires when the engine spell-shield gate (Spell.ApplyEffects → AttackableUnit.
        // ConsumeSpellShield) blocks a hostile spell execution with an active SPELL_SHIELD buff.
        // Keyed by the shield BUFF; data = the blocked spell. The shield's buff script does its
        // on-block reaction here (self-removal, on-block FX, Sivir-E mana). If no handler
        // deactivates the shield buff, ConsumeSpellShield force-removes it as fallback.
        // Engine-convenience event: Riot's server-internal notification path is unknown — replays
        // show only the shield's BuffRemove2 plus server-sent on-block FX at the consume instant
        // (project_spell_shield_system memory, replay-verified 2026-07-05).
        public static Dispatcher<Buff, Spell> OnSpellShieldBroken
            = new Dispatcher<Buff, Spell>();

        public static Dispatcher<Shield> OnShieldBreak
            = new Dispatcher<Shield>();

        // Engine-level shield lifecycle events. NOTE: these are NOT faithful Riot buff callbacks
        // (Riot has no OnShield* handler — a shield there IS a buff, so its lifecycle is the buff's
        // OnActivate/OnPreDamage/OnDeactivate). They mirror this engine's dedicated Shield-object
        // system, extending the same convenience pattern as OnShieldBreak above.

        // Fires when a shield is added to a unit. Keyed by the RECEIVING unit (the Shield does not
        // exist yet at subscribe time), carrying the new Shield as data.
        public static Dispatcher<AttackableUnit, Shield> OnShieldAdded
            = new Dispatcher<AttackableUnit, Shield>();

        // Fires when an existing shield's amount grows (IncShield). Keyed by the Shield, carrying
        // the applied delta.
        public static Dispatcher<Shield, float> OnShieldIncreased
            = new Dispatcher<Shield, float>();

        // Fires when a shield's amount shrinks — damage consumption (ConsumeShields) or ReduceShield.
        // Keyed by the Shield, carrying the amount removed. A killing drain fires this (with the
        // consumed delta) and then OnShieldBreak from RemoveShield when it hits 0.
        public static Dispatcher<Shield, float> OnShieldReduced
            = new Dispatcher<Shield, float>();
        
        public static ConditionDispatcher<ObjAIBase, OrderType> OnUnitUpdateMoveOrder
            = new ConditionDispatcher<ObjAIBase, OrderType>();

        public static Dispatcher<AttackableUnit, float> OnUpdateStats
            = new Dispatcher<AttackableUnit, float>();

        // Fires when a spell's ammo (charge) count changes. Faithful to Riot's
        // obj_AI_Base::HandleOnAmmoUpdate(numStacks=currentAmmoCount, spellSlot) → buff-script
        // BuffOnUpdateAmmo (decomp HandleUpdateAmmoBuff(ownerID, stacks, spellSlot)). Published
        // only on an actual count change — recharge (+1), restore, or cast-consume (-1) — never
        // per-tick. The Spell argument carries the owner, slot (CastInfo.SpellSlot) and CurrentAmmo.
        public static Dispatcher<ObjAIBase, Spell> OnUpdateAmmo
            = new Dispatcher<ObjAIBase, Spell>();

        public static Dispatcher<Spell, SpellCastInfo> OnSpellPress
            = new Dispatcher<Spell, SpellCastInfo>();

        public static Dispatcher<AttackableUnit, StatsModifier> OnStatModified
            = new Dispatcher<AttackableUnit, StatsModifier>();

        public static Dispatcher<AttackableUnit> OnEnterGrass
            = new Dispatcher<AttackableUnit>();

        public static Dispatcher<AttackableUnit> OnLeaveGrass
            = new Dispatcher<AttackableUnit>();

        public abstract class DispatcherBase
        {
            public DispatcherBase()
            {
                _dispatchers.Add(this);
            }

            public abstract void RemoveListener(object owner);
        }

        public abstract class DispatcherBase<Source, CBType> : DispatcherBase
        {
            protected class Listener
            {
                public object Owner;
                public Source Source;
                public CBType Callback;
                public bool SingleInstance;
                // Cached "TypeName.MethodName" for the Profiler so we don't
                // re-resolve MethodInfo via reflection on every Publish.
                public string ProfileName;

                public Listener(object owner, Source source, CBType callback, bool singleInstance = false)
                {
                    Owner = owner;
                    Source = source;
                    Callback = callback;
                    SingleInstance = singleInstance;
                    // Build a profile name that tells the operator which
                    // *concrete game thing* is running, not just which C# class.
                    // Many spells share BaseSpell.OnUpdate via inheritance, so
                    // "script:BaseSpell.OnUpdate" alone is useless — prefer the
                    // data name from the source (SpellName / Buff Name) when we
                    // can recognise it. Falls through to the delegate's
                    // owner/declaring type for unknown source kinds.
                    var del = (object)callback as Delegate;
                    var methodName = del?.Method.Name ?? "<unknown>";
                    string label = source switch
                    {
                        // For spells, include the owner unit so 30+ placeholder
                        // "BaseSpell" slots per champion are distinguishable
                        // (e.g. "spell:Ezreal/BaseSpell" vs "spell:LaneTurret/BaseSpell").
                        Spell s => $"spell:{s.CastInfo?.Owner?.Model ?? "?"}/{s.SpellName}",
                        Buff b => $"buff:{b.Name}",
                        _ => del?.Target?.GetType().Name is string t
                             ? $"script:{t}"
                             : del?.Method.DeclaringType?.Name is string d
                                 ? $"script:{d}"
                                 : "script:<unknown>",
                    };
                    ProfileName = $"{label}.{methodName}";
                }
            }

            protected readonly List<Listener> _listeners = new List<Listener>();

            // Storage for Publish functions counters.
            protected List<int> _stack = new List<int> { -1, -1, -1, -1, -1, -1, -1, -1 };

            // The index of the last Publish function currently executing.
            protected int _nestingLevel = -1;

            protected void IncrementNestingLevel()
            {
                _nestingLevel++;
                if (_nestingLevel >= _stack.Count)
                {
                    _stack.Add(-1);
                }
            }

            // Removes the element and adjusts the counters of all currently executing Publish functions, if necessary.
            protected void CarefulRemoval(int index)
            {
                _listeners.RemoveAt(index);
                for (int l = 0; l < _nestingLevel + 1; l++)
                {
                    if (index < _stack[l])
                    {
                        _stack[l]--;
                    }
                }
            }

            private void CarefulRemoval(Predicate<Listener> match)
            {
                for (int j = _listeners.Count - 1; j >= 0; j--)
                {
                    var listener = _listeners[j];
                    if (match(listener))
                    {
                        CarefulRemoval(j);
                    }
                }
            }

            public void AddListener(object owner, Source source, CBType callback, bool singleInstance = false)
            {
                if (owner == null || source == null || callback == null)
                {
                    return;
                }

                _listeners.Add(
                    new Listener(owner, source, callback, singleInstance)
                );
            }

            public override void RemoveListener(object owner)
            {
                CarefulRemoval(listener => listener.Owner == owner);
            }

            public void RemoveListener(object owner, Source source)
            {
                CarefulRemoval(listener => listener.Owner == owner && listener.Source.Equals(source));
            }

            public void RemoveListener(object owner, Source source, CBType callback)
            {
                CarefulRemoval(listener =>
                    listener.Owner == owner && listener.Source.Equals(source) && listener.Callback.Equals(callback));
            }
        }

        public abstract class VariableDispatcherBase<Source, Data, CBType> : VariableDispatcherBase<Source, CBType>
        {
            protected Data _data;

            public void Publish(Source source, Data data)
            {
                _data = data;
                base.Publish(source);
            }
        }

        public abstract class VariableDispatcherBase<Source, CBType> : DispatcherBase<Source, CBType>
        {
            protected Source _source;
            protected abstract void Call(CBType callback);

            protected void Publish(Source source)
            {
                IncrementNestingLevel();
                _source = source;

                int i;
                for (
                    _stack[_nestingLevel] = _listeners.Count - 1;
                    (i = _stack[_nestingLevel]) >= 0;
                    _stack[_nestingLevel]--
                )
                {
                    var listener = _listeners[i];
                    if (listener.Source.Equals(source))
                    {
                        if (listener.SingleInstance)
                        {
                            CarefulRemoval(i);
                        }

                        try
                        {
                            using var _scope = Profiler.Scope(listener.ProfileName, "scripts");
                            Call(listener.Callback);
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e);
                        }
                    }
                }

                _nestingLevel--;
            }
        }

        public abstract class ConditionDispatcherBase<Source, Data, CBType> : DispatcherBase<Source, CBType>
        {
            protected Source _source;
            protected Data _data;
            protected abstract bool Call(CBType callback);

            public bool Publish(Source source, Data data)
            {
                IncrementNestingLevel();
                _source = source;
                _data = data;

                bool returnVal = true;
                int i;
                for (
                    _stack[_nestingLevel] = _listeners.Count - 1;
                    (i = _stack[_nestingLevel]) >= 0;
                    _stack[_nestingLevel]--
                )
                {
                    var listener = _listeners[i];
                    if (listener.Source.Equals(source))
                    {
                        if (listener.SingleInstance)
                        {
                            CarefulRemoval(i);
                        }

                        try
                        {
                            using var _scope = Profiler.Scope(listener.ProfileName, "scripts");
                            returnVal = returnVal && Call(listener.Callback);
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e);
                        }
                    }
                }

                _nestingLevel--;
                return returnVal;
            }
        }

        public class Dispatcher<Source> : VariableDispatcherBase<Source, Action<Source>>
        {
            public new void Publish(Source source)
            {
                base.Publish(source);
            }

            protected override void Call(Action<Source> callback)
            {
                callback(_source);
            }
        }

        public class Dispatcher<Source, Data> : VariableDispatcherBase<Source, Data, Action<Source, Data>>
        {
            protected override void Call(Action<Source, Data> callback)
            {
                callback(_source, _data);
            }
        }

        public class DataOnlyDispatcher<Source, Data> : VariableDispatcherBase<Source, Data, Action<Data>>
        {
            protected override void Call(Action<Data> callback)
            {
                callback(_data);
            }
        }

        public class
            Dispatcher<Source, D1, D2> : VariableDispatcherBase<Source, (D1, D2), Action<Source, D1, D2>>
        {
            protected override void Call(Action<Source, D1, D2> callback)
            {
                callback(_source, _data.Item1, _data.Item2);
            }
        }

        public class
            Dispatcher<Source, D1, D2, D3> : VariableDispatcherBase<Source, (D1, D2, D3), Action<Source, D1, D2, D3>>
        {
            protected override void Call(Action<Source, D1, D2, D3> callback)
            {
                callback(_source, _data.Item1, _data.Item2, _data.Item3);
            }
        }

        public class
            Dispatcher<Source, D1, D2, D3, D4> : VariableDispatcherBase<Source, (D1, D2, D3, D4),
            Action<Source, D1, D2, D3, D4>>
        {
            protected override void Call(Action<Source, D1, D2, D3, D4> callback)
            {
                callback(_source, _data.Item1, _data.Item2, _data.Item3, _data.Item4);
            }
        }

        public class ConditionDispatcher<Source, Data> : ConditionDispatcherBase<Source, Data, Func<Source, Data, bool>>
        {
            protected override bool Call(Func<Source, Data, bool> callback)
            {
                return callback(_source, _data);
            }
        }

        public class
            ConditionDispatcher<Source, D1, D2> : ConditionDispatcherBase<Source, (D1, D2), Func<Source, D1, D2, bool>>
        {
            protected override bool Call(Func<Source, D1, D2, bool> callback)
            {
                return callback(_source, _data.Item1, _data.Item2);
            }
        }
    }
}
