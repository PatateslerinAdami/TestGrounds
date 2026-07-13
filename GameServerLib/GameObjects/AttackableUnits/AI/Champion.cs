using GameServerCore.Enums;
using GameServerCore.NetInfo;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using GameServerLib.Handlers;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Quests;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public class Champion : ObjAIBase
    {
        private float _championHitFlagTimer;
        private static ILog _logger = LoggerProvider.GetLogger();

        // Champions use the client's fast A* mode (mTravelFactor=2.5, hint multiplier=6.0).
        // Mirrors the client default for `Actor_Common`-derived entities that don't override
        // `m_UseSlowerButMoreAccurateSearch` (S1 actor_client.cpp:4109 sets default 0 = fast).
        // Only `obj_AI_Minion` (= our `Minion`/`Pet` subclasses) overrides to slow-accurate.
        public override bool UsesFastPath => true;
        /// <summary>
        /// Player number ordered by the config file.
        /// </summary>
        public int ClientId { get; private set; }
        private uint _playerHitId;
        public Shop Shop { get; protected set; }
        public float RespawnTimer { get; private set; }
        public int DeathSpree { get; set; } = 0;
        public int KillSpree { get; set; } = 0;
        public float GoldFromMinions { get; set; }
        public RuneCollection RuneList { get; }
        public TalentInventory TalentInventory { get; set; }
        public ChampionStats ChampStats { get; private set; } = new ChampionStats();

        public byte SkillPoints { get; set; }

        public override bool SpawnShouldBeHidden => false;

        // Set by the !changechampion cheat: this Champion object replaced a previous one on the
        // SAME NetID mid-game. OnSpawn then sends S2C_CreateHero with the ChangeHero bit (client
        // reuses its existing hero entity; falls back to a fresh create if it has none — safe for
        // reconnects) plus S2C_ChangeCharacterData(ReplaceCharacterPackage) to swap model+spells.
        // Stays true for the rest of the game so late spawns/reconnects take the same safe path.
        public bool SpawnAsChangeHero { get; set; }
        /// <summary>
        /// Zero-based spawn slot within the team (0-4), set at AddPlayer time. Goes on the wire
        /// as S2C_CreateHero.SpawnPositionIndex — replay-verified: every champion carries a
        /// team-unique 0-4 index (the fountain-platform formation slot).
        /// </summary>
        public int SpawnIndex { get; set; }

        public List<EventHistoryEntry> EventHistory { get; } = new List<EventHistoryEntry>();
        public bool teamChanged = false;
        public PlayerQuestManager PlayerQuestManager { get; private set; }

        public Champion(Game game,
                        string model,
                        RuneCollection runeList,
                        TalentInventory talentInventory,
                        ClientInfo clientInfo,
                        uint netId = 0,
                        TeamId team = TeamId.TEAM_BLUE,
                        Stats stats = null,
                        string AIScript = "")
            // Default player champions to the minimal HeroAI so they run the shared
            // CrowdControlComponent (AI-driven fear/flee, Riot's uniform model). Bots that were given
            // a specific AI script keep it. See AIScripts/HeroAI.cs, project_cc_model_architecture.
            : base(game, model, clientInfo.Name, 30, new Vector2(), 1200, clientInfo.SkinNo, netId, team, stats,
                string.IsNullOrEmpty(AIScript) ? "HeroAI" : AIScript)
        {
            //TODO: Champion.ClientInfo?
            ClientId = clientInfo.ClientId;
            RuneList = runeList;

            TalentInventory = talentInventory;
            Shop = Shop.CreateShop(this, game);

            AddGold(null, GlobalData.ObjAIBaseVariables.StartingGold, false);
            Stats.GoldPerGoldTick.BaseValue = GlobalData.ChampionVariables.AmbientGoldAmount;
            Stats.IsGeneratingGold = false;

            //TODO: automaticaly rise spell levels with CharData.SpellLevelsUp

            Spells[(int)SpellSlotType.SummonerSpellSlots] = new Spell(game, this, clientInfo.SummonerSkills[0], (int)SpellSlotType.SummonerSpellSlots);
            Spells[(int)SpellSlotType.SummonerSpellSlots].LevelUp();
            Spells[(int)SpellSlotType.SummonerSpellSlots + 1] = new Spell(game, this, clientInfo.SummonerSkills[1], (int)SpellSlotType.SummonerSpellSlots + 1);
            Spells[(int)SpellSlotType.SummonerSpellSlots + 1].LevelUp();

            Spells[(int)SpellSlotType.BluePillSlot] = new Spell(game, this,
            _game.ItemManager.GetItemType(_game.Map.MapScript.MapScriptMetadata.RecallSpellItemId).SpellName, (int)SpellSlotType.BluePillSlot);
            Stats.SetSpellEnabled((byte)SpellSlotType.BluePillSlot, true);
            SkillPoints++;

            Replication = new ReplicationHero(this);


            if (clientInfo.PlayerId == -1)
            {
                // Bot (no real client). Riot flags bots IsBot=true on S2C_CreateHero (replay-verified);
                // the client renders the name as "(Champion) Bot". Faithful over the custom-name gimmick.
                IsBot = true;
            }
            PlayerQuestManager = new PlayerQuestManager(game, this);
        }

        public void AddGold(AttackableUnit source, float gold, bool notify = true)
        {
            Stats.Gold += gold;
            if (notify && source != null)
            {
                _game.PacketNotifier.NotifyUnitAddGold(this, source, gold);
            }
        }

        public void AddAmountToCreepScore(int amount, AttackableUnit killedMinion)
        {
            ChampStats.MinionsKilled += amount;

            // Send a second death notification crediting this champ
            // so the client updates the CS counter on the HUD
            var fakeDeathData = new DeathData
            {
                Unit = killedMinion,
                Killer = this,
                DamageType = DamageType.DAMAGE_TYPE_TRUE,
                DamageSource = DamageSource.DAMAGE_SOURCE_PROC,
            };
            _game.PacketNotifier.NotifyDeath(fakeDeathData);
        }

        public override void OnAdded()
        {
            _game.ObjectManager.AddChampion(this);
            base.OnAdded();
            TalentInventory.Initialize(this);

            var bluePill = _itemManager.GetItemType(_game.Map.MapScript.MapScriptMetadata.RecallSpellItemId);
            Inventory.SetExtraItem(7, bluePill);

            // Runes
            byte runeItemSlot = 14;
            foreach (var rune in RuneList.Runes)
            {
                var runeItem = _itemManager.GetItemType(rune.Value);
                var newRune = Inventory.SetExtraItem(runeItemSlot, runeItem);
                AddStatModifier(runeItem);
                runeItemSlot++;
            }
            Stats.SetSummonerSpellEnabled(0, true);
            Stats.SetSummonerSpellEnabled(1, true);

            //Change this to send only a single LevelUp call in case of multiple levels.
            while (Stats.Level < _game.Map.MapScript.MapScriptMetadata.InitialLevel)
            {
                LevelUp(true);
            }
        }

        protected override void OnSpawn(int userId, TeamId team, bool doVision)
        {
            var peerInfo = _game.PlayerManager.GetClientInfoByChampion(this);
            // NOTE: bots spawn via the normal S2C_CreateHero path too. The dedicated SpawnBotS2C (0xCF)
            // packet was tried (NotifyS2C_SpawnBot) but the 4.20 client mis-positions the bot from it
            // (turret homed off-map) — it's a pre-4.18 path (BotRank deprecated) the client no longer
            // positions heroes from. CreateHero renders bots correctly, so we keep it.
            _game.PacketNotifier.NotifyS2C_CreateHero(peerInfo, userId, doVision, SpawnAsChangeHero);
            if (SpawnAsChangeHero)
            {
                // The ChangeHero CreateHero only re-binds the client's existing entity; the actual
                // model+spell swap is the character-package replace (Elise-style form swaps use the
                // same packet with the package kept — replay: 0x97 carries "EliseSpider").
                _game.PacketNotifier.NotifyS2C_ChangeCharacterData(this, userId, (uint)SkinID,
                    modelOnly: false, overrideSpells: true, replaceCharacterPackage: true);
            }
            _game.PacketNotifier.NotifyAvatarInfo(peerInfo, userId);

            bool ownChamp = peerInfo.ClientId == userId;
            if (ownChamp)
            {
                // Buy blue pill
                var itemInstance = Inventory.GetItem(7);
                _game.PacketNotifier.NotifyBuyItem(this, itemInstance);

                // Set spell levels
                foreach (var spell in Spells)
                {
                    var castInfo = spell.Value.CastInfo;
                    if (castInfo.SpellLevel > 0)
                    {
                        // NotifyNPC_UpgradeSpellAns has no effect here
                        _game.PacketNotifier.NotifyS2C_SetSpellLevel(userId, NetId, castInfo.SpellSlot, castInfo.SpellLevel);

                        float currentCD = spell.Value.CurrentCooldown;
                        float totalCD = spell.Value.GetCooldown();
                        if (currentCD > 0)
                        {
                            _game.PacketNotifier.NotifyCHAR_SetCooldown(this, castInfo.SpellSlot, currentCD, totalCD, userId);
                        }
                    }
                }
            }
        }

        public override void OnRemoved()
        {
            base.OnRemoved();
            _game.ObjectManager.RemoveChampion(this);
        }

        public int GetTeamSize()
        {
            var teamSize = 0;
            foreach (var player in _game.Config.Players)
            {
                if (player.Team == Team)
                {
                    teamSize++;
                }
            }
            return teamSize;
        }

        public Vector2 GetSpawnPosition(int index)
        {
            var teamSize = GetTeamSize();

            if (_game.Map.PlayerSpawnPoints[Team].ContainsKey(teamSize))
            {
                return _game.Map.PlayerSpawnPoints[Team][teamSize][index];
            }

            if (_game.Map.PlayerSpawnPoints[Team].ContainsKey(1) && _game.Map.PlayerSpawnPoints[Team][1].ContainsKey(1))
            {
                return _game.Map.PlayerSpawnPoints[Team][1][1];
            }
            //TODO: wrap in try {} catch
            return _game.Map.MapScript.GetFountainPosition(Team);
        }

        public Vector2 GetRespawnPosition()
        {
            //TODO: wrap in try {} catch
            return _game.Map.MapScript.GetFountainPosition(Team);
        }

        public Spell LevelUpSpell(byte slot, bool spendSkillPoint) {
            if (spendSkillPoint && SkillPoints == 0) return null;

            var spell = Spells[slot];
            if (spell == null) return null;

            // Player skill-ups obey Riot's Spell::SpellSlotCanBeUpgraded gate (per-rank champion-level
            // thresholds + rank cap — see CanLevelUpSpell). Script-driven free ranks (spendSkillPoint:
            // false, e.g. Karma's Mantra at spawn) bypass it deliberately.
            if (spendSkillPoint && !CanLevelUpSpell(spell)) return null;

            spell.LevelUp();
            
            if (spell.CastInfo.SpellLevel == 1) {
                Stats.SetSpellEnabled(slot, true);
            }

            ApiEventManager.OnLevelUpSpell.Publish(spell);

            if (spendSkillPoint) {
                SkillPoints--;
            }

            return spell;
        }

        public override Spell LevelUpSpell(byte slot)
        {
            return LevelUpSpell(slot, true);
        }

        float _goldTimer;
        float _EXPTimer;
        public override void Update(float diff)
        {
            base.Update(diff);

            // Shop upkeep: clears the undo stack once the champion leaves the fountain (see Shop.OnUpdate).
            Shop.OnUpdate(diff);

            if (Stats.IsGeneratingGold && Stats.GoldPerGoldTick.Total > 0)
            {
                _goldTimer -= diff;

                if (_goldTimer <= 0)
                {
                    AddGold(null, Stats.GoldPerGoldTick.Total, false);
                    _goldTimer = GlobalData.ChampionVariables.AmbientGoldInterval;
                }
            }
            else if (!Stats.IsGeneratingGold && _game.GameTime >= GlobalData.ObjAIBaseVariables.AmbientGoldDelay)
            {
                Stats.IsGeneratingGold = true;
                _logger.Debug("Generating Gold!");
            }

            if (_game.GameTime >= GlobalData.ChampionVariables.AmbientXPDelay)
            {
                _EXPTimer -= diff;
                if (_EXPTimer <= 0)
                {
                    AddExperience(GlobalData.ChampionVariables.AmbientXPAmount, false);
                    _EXPTimer = GlobalData.ChampionVariables.AmbientXPInterval;
                }
            }



            if (RespawnTimer > 0)
            {
                RespawnTimer -= diff;
            }
            // Respawn when the timer expires. During the zombie phase IsDead is false (Model B), so a
            // zombie never respawns mid-phase; EndZombie() sets IsDead=true at the real death and the
            // (already counted-down) timer then completes here. The timer runs from death to match
            // the client HUD.
            if (IsDead && RespawnTimer <= 0)
            {
                Respawn();
            }

            if (_championHitFlagTimer > 0)
            {
                _championHitFlagTimer -= diff;
                if (_championHitFlagTimer <= 0)
                {
                    _championHitFlagTimer = 0;
                }
            }

            // Tooltip-var changes are flushed game-wide in ONE bulk S2C_ToolTipVars per tick
            // (Game.Update end) — replay-verified Riot shape (cross-owner blocks, incl. non-champions).
        }

        public void Respawn()
        {
            // Safety: clear any lingering zombie state so a respawned champion is never a zombie.
            IsZombie = false;
            _zombieDeath = null;
            var spawnPos = GetRespawnPosition();
            SetPosition(spawnPos);
            float parToRestore = 0;
            // TODO: Find a better way to do this, perhaps through scripts. Otherwise, make sure all types are accounted for.
            if (Stats.ParType == PrimaryAbilityResourceType.Mana || Stats.ParType == PrimaryAbilityResourceType.Energy || Stats.ParType == PrimaryAbilityResourceType.Wind)
            {
                parToRestore = Stats.ManaPoints.Total;
            }
            Stats.CurrentMana = parToRestore;
            _game.PacketNotifier.NotifyHeroReincarnateAlive(this, parToRestore);
            Stats.CurrentHealth = Stats.HealthPoints.Total;
            IsDead = false;
            // Back at the fountain and alive: drop the dead-state force-enable (normal location gate).
            Shop.SetShopState(true, false);
            RespawnTimer = -1;
            SetForceMovementState(false, MoveStopReason.HeroReincarnate);
            // Riot fires the HeroReincarnate channel-stop imperatively from the reincarnate path
            // (AIHero::DoReincarnateHeroAlive, AIHero.cpp:916) — not from the per-tick cancel
            // check. Covers a channel that survived death into the respawn (belt against
            // channel-state leaks; normally the Die stop already ended it).
            if (ChannelSpell != null)
            {
                StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.HeroReincarnate);
            }
            ApiEventManager.OnResurrect.Publish(this);
            SetCastSpell(null);
        }

        public bool OnDisconnect()
        {
            this.StopMovement();
            this.SetWaypoints(_game.Map.PathingHandler.GetPath(Position, GetRespawnPosition(), PathfindingRadius, UsesFastPath));
            this.UpdateMoveOrder(OrderType.MoveTo, true);

            return true;
        }

        public void Recall()
        {
            var spawnPos = GetRespawnPosition();
            TeleportTo(spawnPos.X, spawnPos.Y);
        }

        public void AddExperience(float experience, bool notify = true)
        {
            if (experience > 0)
            {
                // Percent EXP bonus (Riot AIHero::GetPercentEXPBonus = CharInter.mPercentEXPBonus,
                // fed additively by item/rune "PercentEXPBonus" — in 4.20 only Quint of Experience).
                // Clamped by gcd_PercentEXPBonusMinimum/Maximum (Constants.var, GlobalCharacterData
                // .cpp:50) — the server-side multiply point is not in the client corpora, so applying
                // it at the grant with the gcd clamp is the evidenced-shape implementation.
                float expBonus = Math.Clamp(Stats.PercentEXPBonus.Total,
                    GlobalData.GlobalCharacterDataConstants.PercentEXPBonusMinimum,
                    GlobalData.GlobalCharacterDataConstants.PercentEXPBonusMaximum);
                if (expBonus != 0f)
                {
                    experience *= 1f + expBonus;
                }

                Stats.Experience += experience;

                if (notify)
                {
                    _game.PacketNotifier.NotifyUnitAddEXP(this, experience);
                }

                while (Stats.Experience >= _game.Map.MapData.ExpCurve[Stats.Level - 1] && LevelUp()) ;
            }
        }

        public override bool LevelUp(bool force = false)
        {
            var stats = Stats;
            var expMap = _game.Map.MapData.ExpCurve;

            if (force && stats.Level > 0)
            {
                Stats.Experience = expMap[Stats.Level - 1];
            }

            if (stats.Level < _game.Map.MapScript.MapScriptMetadata.MaxLevel && (stats.Level < 1 || (stats.Experience >= expMap[stats.Level - 1])))
            {
                if (stats.Level <= 18)
                {
                    SkillPoints++;
                }
                base.LevelUp(force);
                _logger.Debug($"Player {Name} leveled up to {stats.Level}");

                return true;
            }

            return false;
        }

        public void OnKill(DeathData deathData)
        {
            ApiEventManager.OnKillUnit.Publish(deathData.Killer, deathData);

            if (deathData.Unit is Minion)
            {
                ApiEventManager.OnMinionKill.Publish(deathData.Killer, deathData);
                ChampStats.MinionsKilled += 1;
                if (deathData.Unit.Team == TeamId.TEAM_NEUTRAL)
                {
                    ChampStats.NeutralMinionsKilled += 1;
                }
                else if (deathData.Unit is LaneMinion)
                {
                    // Lane CS is reconstructed client-side (NOT replicated): players who saw the minion
                    // die count it from the vision-gated NPC_Die; tell the rest explicitly so their
                    // scoreboard CS for this champion stays correct. (Runs before NotifyDeath in Die(),
                    // so the minion's SpawnedForPlayers set still reflects who will get the death.)
                    _game.PacketNotifier.NotifyS2C_IncrementMinionKills(this, deathData.Unit);
                }

                var gold = deathData.Unit.Stats.GoldGivenOnDeath.Total;
                if (gold <= 0)
                {
                    return;
                }

                AddGold(deathData.Unit, gold);

                if (DeathSpree > 0)
                {
                    GoldFromMinions += gold;
                }

                if (GoldFromMinions >= 1000)
                {
                    GoldFromMinions -= 1000;
                    DeathSpree -= 1;
                }
            }
        }

        public override void Die(DeathData data)
        {
            IsDead = true;
            // Dead champions may still shop (recall-less fountain shopping): force-enable the shop so
            // the location gate is bypassed. Re-disabled on respawn. See docs/SHOP_PACKETS_PLAN.md.
            Shop.SetShopState(true, true);
            ChampStats.Deaths++;

            _game.ObjectManager.StopTargeting(this);
            SetForceMovementState(false, MoveStopReason.Death);
            // Stop and clear the current path (Riot DoDeath zeroes Velocity + AI_actor.HandleDeath).
            // Without this a champion that dies mid-move keeps sliding to its last waypoint — visible
            // for zombies (Karthus Death Defied), which stay in the world instead of being removed.
            StopMovement(MoveStopReason.Death);
            // OnDeath before the zombie decision (Riot DoDeath ordering) so a death-reactive buff
            // (Karthus DeathDefied) can arm data.BecomeZombie from its OnDeath handler.
            ApiEventManager.OnDeath.Publish(data.Unit, data);
            data.Unit.SetStatus(StatusFlags.Ghosted, true);

            if (data.Killer is Champion)
            {
                ChampionDeathHandler.ProcessKill(data);
            }
            else if (EnemyAssistMarkers.LastOrDefault()?.Source is Champion ch)
            {
                data.Killer = ch;
                ChampionDeathHandler.ProcessKill(data);
            }

            // Kill credit fires immediately (the killing blow earns the kill even if the victim
            // lingers as a zombie).
            EventHistory.Clear();
            ApiEventManager.OnKill.Publish(data.Killer, data);

            // Replay-verified (Karthus rlp f3ad103e): the die packet + death timer are sent AT death
            // with the real respawn duration — even for a zombie. The client keeps the model and
            // champion indicator and skips ONLY the death animation when BecomeZombie is set
            // (AIHeroClient::DoDeath gates those on !bBecomeZombie) while still showing the death
            // screen + death timer. The earlier model/HP-bar loss was a NEGATIVE DeathDuration
            // (RespawnTimer was still -1 when the packet was sent), which the client treats as a
            // completed/forced death. Set a valid respawn time first.
            RespawnTimer = _game.Config.GameFeatures.HasFlag(FeatureFlags.EnableDeathTimer)
                ? Stats.GetRespawnTimer(_game.Map.MapData.GetDeathTime(Stats.Level, _game.GameTime / 1000.0f) * 1000.0f)
                : 100.0f;

            if (data.BecomeZombie)
            {
                // Zombie champion (Karthus Death Defied). Model B (faithful to Riot DoDeath, which
                // sets bZombie but NOT the dead flag): a zombie is NOT counted as dead — clear IsDead
                // so it behaves like a live unit (moves, holds vision, is targetable) until its real
                // death in EndZombie(). The die packet + death timer were already sent above; the
                // respawn countdown runs from here but Respawn() only fires once IsDead is true again.
                IsZombie = true;
                IsDead = false;
                _zombieDeath = data;
                ApiEventManager.OnZombie.Publish(data.Unit, data);
            }

            _game.PacketNotifier.NotifyNPC_Hero_Die(data);
        }

        /// <summary>
        /// Ends a zombie champion's phase (deferred from <see cref="Die"/>) and starts the real
        /// respawn countdown. Champions are never SetToRemove'd, so this overrides the base
        /// removal-based <see cref="AttackableUnit.EndZombie"/>.
        /// </summary>
        public override void EndZombie()
        {
            if (!IsZombie)
            {
                return;
            }

            // The zombie truly dies now: clear the zombie state and set IsDead so the (already
            // running) respawn countdown can complete in OnUpdate.
            IsZombie = false;
            IsDead = true;
            _zombieDeath = null;

            // Replay-verified (Karthus rlp f3ad103e): at the zombie→real-death boundary the server
            // sends NPC_ForceDead (0x1B). That drives the client's DoForceDead → SetDeathScreen(true)
            // + the HUD death timer (with the remaining respawn duration). The BecomeZombie die packet
            // sent at Die() only puts the client into the controllable zombie state; THIS is what
            // flips it into the real grey-screen death.
            _game.PacketNotifier.NotifyNPC_ForceDead(this, RespawnTimer / 1000f);
        }

        private T CreateEventForHistory<T>(AttackableUnit source, IEventSource sourceScript) where T: ArgsForClient, new()
        {
            if(source == null || sourceScript == null)
            {
                return null;
            }

            var entry = new EventHistoryEntry();
            entry.Timestamp = _game.GameTime / 1000f; // ?
            entry.Count = 1; //TODO: stack?
            entry.Source = source.NetId;
            var e = new T();
            entry.Event = (IEvent)e;

            e.ParentCasterNetID = entry.Source;
            e.OtherNetID = this.NetId;

            e.ScriptNameHash = 1;
            e.ParentScriptNameHash = sourceScript.ScriptNameHash;
            if(sourceScript.ParentScript != null)
            {
                e.ScriptNameHash = sourceScript.ScriptNameHash;
                e.ParentScriptNameHash = sourceScript.ParentScript.ScriptNameHash;
            }
            else if(sourceScript is Buff b && b.OriginSpell != null)
            {
                e.ScriptNameHash = sourceScript.ScriptNameHash;
                e.ParentScriptNameHash = (uint)b.OriginSpell.GetId();
            }

            e.EventSource = 0; // ?
            e.Unknown = 0; // ?
            e.SourceObjectNetID = 0;
            e.Bitfield = 0; // ?

            EventHistory.Add(entry);

            return e;
        }

        public override bool AddBuff(Buff b)
        {
            if(base.AddBuff(b))
            {
                CreateEventForHistory<OnBuff>(b.SourceUnit, b);
                return true;
            }
            return false;
        }

        public override void TakeHeal(AttackableUnit caster, float amount, HealType healType, IEventSource sourceScript = null)
        {
            var healerUnit = caster ?? this;
            var previousHealth = Stats.CurrentHealth;
            base.TakeHeal(healerUnit, amount, healType, sourceScript);
            var actualHeal = Stats.CurrentHealth - previousHealth;

            if (actualHeal > 0.0f
                && healerUnit is Champion healer
                && healer != this
                && healer.Team == Team)
            {
                AddAssistMarker(healer, GlobalData.ChampionVariables.TimerForAssist);
            }

            var e = CreateEventForHistory<OnCastHeal>(healerUnit, sourceScript);
            if (e != null)
            {
                e.HealAmmount = actualHeal;
            }
        }

        public override void TakeDamage(DamageData damageData, DamageResultType damageText, IEventSource sourceScript = null)
        {
            base.TakeDamage(damageData, damageText, sourceScript);

            _championHitFlagTimer = 15 * 1000; //15 seconds timer, so when you get executed the last enemy champion who hit you gets the gold
            _playerHitId = damageData.Attacker.NetId;
            //CORE_INFO("15 second execution timer on you. Do not get killed by a minion, turret or monster!");

            var e = CreateEventForHistory<OnDamageGiven>(damageData.Attacker, sourceScript);
            if(e != null)
            {
                if(damageData.DamageType == DamageType.DAMAGE_TYPE_MAGICAL)
                {
                    e.MagicalDamage = damageData.PostMitigationDamage;
                }
                else if(damageData.DamageType == DamageType.DAMAGE_TYPE_PHYSICAL)
                {
                    e.PhysicalDamage = damageData.PostMitigationDamage;
                }
                else if(damageData.DamageType == DamageType.DAMAGE_TYPE_TRUE)
                {
                    e.TrueDamage = damageData.PostMitigationDamage;
                }
                //TODO: handle mixed damage?
            }
        }

        public void UpdateSkin(int skinNo)
        {
            SkinID = skinNo;
        }

        public void IncrementScore(float points, ScoreCategory scoreCategory, ScoreEvent scoreEvent, bool doCallOut, bool notifyText = true)
        {
            Stats.Points += points;
            var scoreData = new ScoreData(this, points, scoreCategory, scoreEvent, doCallOut);
            _game.PacketNotifier.NotifyS2C_IncrementPlayerScore(scoreData);

            if (notifyText)
            {
                //TODO: Figure out what "Params" is exactly
                _game.PacketNotifier.NotifyDisplayFloatingText(new FloatingTextData(this, $"+{(int)points} Points", FloatTextType.Score, 1073741833), Team);
            }

            ApiEventManager.OnIncrementChampionScore.Publish(scoreData.Owner, scoreData);
        }
    }
}
