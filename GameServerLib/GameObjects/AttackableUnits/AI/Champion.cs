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

        // Heroes are the slower-but-more-accurate pathing class: obj_AI_Base::Load passes
        // objIsHeroAI(this) into the Actor flag store (AIBase.cpp:552 → Actor.cpp:2505) —
        // travelFactor 5.0 + distance-based tightness hint, 10000-step A*, 0.2r/0.3r
        // hard/soft body radii, temp-ghost threshold 15. See ObjAIBase.UsesFastPath.
        public override bool UsesFastPath => false;
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
            // Deliberately only the raw client id, NOT a ClientInfo reference (old "Champion.
            // ClientInfo?" TODO): Riot keeps the same split — GetClientID() lives on AttackableUnit
            // (AIAttackableUnit.h:252) while player/connection metadata (PlayerConnectionInfoBase/
            // PlayerLiteInfo) is a separate client-id-keyed system, never a hero member. Callers
            // needing the metadata go through PlayerManager (GetPeerInfo/GetClientInfoByChampion),
            // matching Riot's lookup direction; ClientInfo.Champion stays the single ownership link.
            ClientId = clientInfo.ClientId;
            RuneList = runeList;

            TalentInventory = talentInventory;
            Shop = Shop.CreateShop(this, game);

            AddGold(null, GlobalData.ObjAIBaseVariables.StartingGold, false);
            Stats.GoldPerGoldTick.BaseValue = GlobalData.ChampionVariables.AmbientGoldAmount;
            Stats.IsGeneratingGold = false;

            // No engine auto-ranking from CharData.SpellsUpLevels (an old TODO idea): the tables
            // only GATE skill-ups (CanLevelUpSpell) — no data marker distinguishes auto-ranking
            // ults (Jayce/Elise/Nidalee/Karma, Up4 = {1,6,11,16} + MaxLevels 4) from player-skilled
            // ones with the same table shape (Udyr's R starts at 1 too but takes points). Free
            // ranks are therefore per-champion CHAR SCRIPT logic, Riot-style — see
            // CharScriptKarma.GrantFreeMantraRanks for the pattern (spawn + OnLevelUp grants
            // driven by the same chardata table).

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
            if (notify)
            {
                // source == null still notifies, with wire SourceNetID = 0 — Riot's shape for
                // sourceless grants (replay: turret GLOBAL gold arrives with SourceNetID 0).
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
            // Deliberately NOT wrapped in try/catch: GetFountainPosition can only throw
            // (FountainList[team] KeyNotFound) when the map's fountain objects are missing for a
            // player team — broken map content. Every playable map defines both fountains; a catch
            // here would silently spawn champions at (0,0) inside terrain and mask the data bug.
            return _game.Map.MapScript.GetFountainPosition(Team);
        }

        public Vector2 GetRespawnPosition()
        {
            // No try/catch — see GetSpawnPosition: a missing fountain is broken map data and must
            // fail loudly, not respawn the champion at (0,0).
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

        public override void Update(float diff)
        {
            base.Update(diff);

            // Shop upkeep: clears the undo stack once the champion leaves the fountain (see Shop.OnUpdate).
            Shop.OnUpdate(diff);

            // Ambient gold/XP income moved to ObjAIBase.Update (Riot: obj_AI_Base level) — see there.



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
            // Respawn PAR values, replay-verified per PAR type (HeroReincarnateAlive 0x2F PARValue
            // across all 4.20 replays, cross-referenced with each champion's chardata PARType):
            //   full   — Mana, Energy (=200), Wind (Yasuo flow, level-scaled max)
            //   1.0    — Battlefury (Tryndamere, 42/42 samples) and Rage (Renekton, 6/6): Riot's
            //            odd flat fury seed, NOT 0 — not in any chardata key, replay-derived
            //   keep   — chardata PARDisplayThroughDeath (only Shyvana/ShyvanaDragon in 4.20; her
            //            wire values 0/11.3/18/100 = current fury persists through death)
            //   0      — everything else (None, Heat, Gnarfury, Ferocity, BloodWell)
            float parToRestore;
            if (CharData.PARDisplayThroughDeath)
            {
                parToRestore = Stats.CurrentMana;
            }
            else
            {
                switch (Stats.ParType)
                {
                    case PrimaryAbilityResourceType.Mana:
                    case PrimaryAbilityResourceType.Energy:
                    case PrimaryAbilityResourceType.Wind:
                        parToRestore = Stats.ManaPoints.Total;
                        break;
                    case PrimaryAbilityResourceType.Battlefury:
                    case PrimaryAbilityResourceType.Rage:
                        parToRestore = 1.0f;
                        break;
                    default:
                        parToRestore = 0;
                        break;
                }
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
            PublishNearbyDeath(data);
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

            // Clear the accumulated damage/heal history ONLY after the death packet (which carries it
            // as the death recap) has been serialized and sent — this life's recap is now on the wire,
            // so the next life starts fresh. Clearing before the send left every recap empty.
            EventHistory.Clear();
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
            // Ambient fallback: when the caller didn't name a sourceScript, resolve it from the running
            // script stack the way Riot does (GetDeathRecapEventSource) — a flagged buff frame wins,
            // otherwise the root script that started the chain. An explicit sourceScript always wins
            // (Riot's SetDeathRecapInfo-style override).
            sourceScript ??= ScriptContext.ResolveDeathRecapSource();

            if(source == null || sourceScript == null)
            {
                return null;
            }

            var entry = new EventHistoryEntry();
            entry.Timestamp = _game.GameTime / 1000f; // ?
            // Count = 1 is Riot's wire behavior (replay: 53/54 die-history first entries carry 1) —
            // the server does NOT pre-aggregate repeated hits; the CLIENT's death recap sums counts
            // per (source, scriptNameHash) itself (DeathRecap.cpp:270, abilityInfo->mCount += count).
            entry.Count = 1;
            entry.Source = source.NetId;
            var e = new T();
            entry.Event = (IEvent)e;

            e.ParentCasterNetID = entry.Source;
            e.OtherNetID = this.NetId;

            // Riot: ScriptNameHash = the leaf script that dealt the effect; ParentScriptNameHash = the
            // owning/parent script, defaulting to the leaf itself when there is no parent (EventFrame,
            // LuaSpellScript.cpp:180-182). The client aggregates the recap per (source, ScriptNameHash),
            // so the leaf must carry the real ELF hash — never a sentinel.
            e.ScriptNameHash = sourceScript.ScriptNameHash;
            if (sourceScript.ParentScript != null)
            {
                e.ParentScriptNameHash = sourceScript.ParentScript.ScriptNameHash;
            }
            else if (sourceScript is Buff b && b.OriginSpell != null)
            {
                // OriginSpell is the buff's parent script — use its ELF ScriptNameHash (the wire hash),
                // not its numeric spell id.
                e.ParentScriptNameHash = b.OriginSpell.ScriptNameHash;
            }
            else
            {
                e.ParentScriptNameHash = sourceScript.ScriptNameHash; // self-parent
            }

            // EventSource (EventEnums.h): the leaf script's source class. Derived from the runtime
            // script type here rather than plumbed through IEventSource — the death recap is its only
            // consumer, and the corpus shows this is per-script constant (SPELL/BUFF/BASICATTACK...).
            e.EventSource = (byte)LeafEventSource(sourceScript);

            // SourceObjectNetID stays 0 (invalid) for the general case, matching Riot: SetDeathRecapInfo
            // sets it only for the death-recap-source event (the killing blow). Leaving it 0 keeps
            // NewByte at its faithful default of 1 and routes this event through the client's legacy
            // aggregation path, exactly as the 4.20 replay corpus shows for ~99.8% of entries.
            e.SourceObjectNetID = 0;

            // Bitfield (ParamsDamage/Heal/Buff, AIBase.cpp:1277):
            //   parentEventSource[0:4] | parentTeam[4:8] | parentEventSourceType[8:12] | sourceSpellLevel[12:15]
            var parentSource = sourceScript.ParentScript ?? sourceScript;
            uint parentEventSource = (uint)LeafEventSource(parentSource) & 0xF;
            uint parentTeam = (uint)ToWireTeam(source.Team) & 0xF;
            uint parentSourceType = (uint)ToWireSourceType(source) & 0xF;
            uint sourceSpellLevel = (uint)SourceSpellLevel(sourceScript) & 0x7;
            e.Bitfield = (ushort)(parentEventSource
                | (parentTeam << 4)
                | (parentSourceType << 8)
                | (sourceSpellLevel << 12));

            EventHistory.Add(entry);

            return e;
        }

        // The leaf/parent EventSource class the client shows next to the recap entry. Buff scripts are
        // BUFF; spell scripts are SPELL unless they are the auto-attack spell (then BASICATTACK, which
        // the corpus confirms for genuine basic-attack damage); AbilityInfo carries its own value.
        private static EventSource LeafEventSource(IEventSource script) => script switch
        {
            Buff => EventSource.BUFF,
            Spell spell => spell.CastInfo.IsAutoAttack ? EventSource.BASICATTACK : EventSource.SPELL,
            AbilityInfo ability => ability.EventSource,
            _ => EventSource.UNKNOWN
        };

        // team_e -> TeamType (Riot ToTeamType, r3dGameEnums.h): ORDER=0, CHAOS=1, NEUTRAL=2; anything
        // else (incl. an unset team) maps to VISTEAM_MAX=4 — the value the corpus shows for the default.
        private static uint ToWireTeam(TeamId team) => team switch
        {
            TeamId.TEAM_BLUE => 0u,
            TeamId.TEAM_PURPLE => 1u,
            TeamId.TEAM_NEUTRAL => 2u,
            _ => 4u
        };

        // Unit class -> EventSourceType (EventEnums.h). CLONE (Shaco/LeBlanc/Wukong) is not modelled
        // yet, so those fall through to UNKNOWN.
        private static uint ToWireSourceType(AttackableUnit unit) => unit switch
        {
            Champion => 0u,     // HERO
            BaseTurret => 2u,   // TOWER
            Pet => 3u,          // PET  (checked before Minion: Pet : Minion)
            Minion => 1u,       // MINION
            _ => 5u             // UNKNOWN
        };

        // 0-based spell level driving the recap priority scaling. Available for spell casts and for
        // buffs that carry an OriginSpell; unknown sources report 0 (the corpus plurality).
        private static int SourceSpellLevel(IEventSource script) => script switch
        {
            Spell spell => spell.CastInfo.SpellLevel,
            Buff buff when buff.OriginSpell != null => buff.OriginSpell.CastInfo.SpellLevel,
            _ => 0
        };

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
                // No DAMAGE_TYPE_MIXED case needed: the value exists in Riot's enum (DamageEnums.h
                // MIXED_DAMAGE = 3) but has NO producer — mixed-damage abilities deal separate typed
                // instances (which land here as separate events), our pipeline throws on MIXED in
                // Stats.GetPostMitigationDamage so it can never reach this point, and the Riot client
                // corpus has no MIXED consumer either. Riot's ParamsDamage carries all three fields
                // per event (EventScriptPackets.h:52-54), so the shape could represent it if a
                // producer ever appeared.
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
                // Param semantics (4.17 decomp Tooltip::FloatingText::FillInAndReplace, Tooltip.cpp
                // :1615): the wire Param is substituted for the "IntParam1" token in the (localized)
                // floating-text template — for score texts that's the point amount. Riot sends a
                // localization KEY as Message + the value as Param; we send pre-baked literal text,
                // so the client never substitutes and Param is inert — but pass the points anyway
                // for semantic correctness. (The previous magic 1073741833 = 0x40000009 looked like
                // a NetID copied from a replay sample and meant nothing.)
                _game.PacketNotifier.NotifyDisplayFloatingText(new FloatingTextData(this, $"+{(int)points} Points", FloatTextType.Score, (int)points), Team);
            }

            ApiEventManager.OnIncrementChampionScore.Publish(scoreData.Owner, scoreData);
        }
    }
}
