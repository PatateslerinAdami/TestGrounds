using System.Collections.Generic;
using LeaguePackets.Game.Common;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits
{
    /// <summary>
    /// Server-side port of the client's CharacterDataStack (mac decomp 4.17,
    /// CharacterDataStack.cpp). A per-unit layered model/skin override: a mutable base skin
    /// plus a stack of override layers. <see cref="Push"/> adds a layer and emits
    /// S2C_ChangeCharacterData; <see cref="PopSpecific"/> removes a layer by id and emits
    /// S2C_PopCharacterData; <see cref="PopAll"/> clears all layers and emits
    /// S2C_PopAllCharacterData. The resolved top layer (or the base when empty) is mirrored
    /// onto the owning unit's Model/SkinID so server-side script checks
    /// (e.g. <c>unit.Model == "SwainBird"</c>) stay correct.
    /// <para/>
    /// The client always treats an incoming ChangeCharacterData as a Push and resolves the
    /// displayed model from the top of its own stack, so model/spell-skin can come from
    /// different layers (spells from the topmost <c>overrideSpells</c> layer). Drives champion
    /// transforms (Elise/Shyvana/Swain/...), object-data swaps (Orianna ball) and evolving
    /// skins (Pulsefire Ezreal). See the formchanger-alt-spells note for the wire mechanism.
    /// </summary>
    public class CharacterDataStack
    {
        /// <summary>Wire sentinel (-1 as uint) = "keep the current skinID" (mac decomp Update guard).</summary>
        public const uint KeepSkinID = 0xFFFFFFFF;

        private readonly AttackableUnit _owner;
        private readonly Game _game;
        private readonly List<CharacterStackData> _layers = new();
        // Per-unit monotonic id; the client matches ChangeCharacterData.id <-> PopCharacterData.id
        // within this unit's stack only, so a per-unit counter is sufficient. Starts at 1 so 0
        // stays reserved for the legacy base-change (SetBase) path.
        private uint _nextId = 1;

        // Last resolved values, mirrored onto the owner (avoids redundant re-apply).
        private string _lastTop;
        private uint _lastSkinID = KeepSkinID;

        // Spell skin = the character whose spellbook is active. Mirror of the client's GetSpellSkin():
        // the topmost overrideSpells layer, else the immutable base character (spawn model). Kept
        // separate from BaseSkinName so a pure model swap (ChangeModel, e.g. SwainBird) does NOT
        // change spells.
        private readonly string _baseCharacterName;
        private string _lastSpellSkin;

        public string BaseSkinName { get; private set; }
        public uint BaseSkinID { get; private set; }

        public CharacterDataStack(AttackableUnit owner, Game game, string baseSkin, uint baseSkinID)
        {
            _owner = owner;
            _game = game;
            BaseSkinName = baseSkin;
            BaseSkinID = baseSkinID;
            _lastTop = baseSkin;
            _lastSkinID = baseSkinID;
            _baseCharacterName = baseSkin;
            _lastSpellSkin = baseSkin;
        }

        /// <summary>Active override layers, bottom-to-top (base excluded). For vision-acquire replication.</summary>
        public IReadOnlyList<CharacterStackData> Layers => _layers;

        /// <summary>
        /// Sets the base skin without emitting any packet or re-applying the model — used at unit
        /// construction once the real spawn skinID is known (ObjAIBase assigns SkinID after the
        /// AttackableUnit base ctor has already created this stack).
        /// </summary>
        internal void OverwriteBaseSilently(string skin, uint skinID)
        {
            BaseSkinName = skin;
            BaseSkinID = skinID;
            if (_layers.Count == 0)
            {
                _lastTop = skin;
                _lastSkinID = skinID;
            }
        }

        /// <summary>True when no override layer is active (the base skin is what's displayed).</summary>
        public bool IsEmpty => _layers.Count == 0;

        /// <summary>
        /// Replaces the base skin (the legacy ChangeModel path). When no override layer is active the
        /// base is the displayed model, so a ChangeCharacterData (base slot id 0) is emitted — this
        /// reproduces the old ChangeModel overwrite exactly (the client pushes it; top resolves to it).
        /// If override layers are present the base change only takes effect once they are popped.
        /// </summary>
        public bool SetBase(string skin, int skinID = -1)
        {
            uint resolvedSkinID = skinID < 0 ? BaseSkinID : (uint)skinID;
            if (BaseSkinName == skin && BaseSkinID == resolvedSkinID)
            {
                return false;
            }
            BaseSkinName = skin;
            BaseSkinID = resolvedSkinID;
            if (_layers.Count == 0)
            {
                _game.PacketNotifier.NotifyS2C_ChangeCharacterData(_owner, new CharacterStackData
                {
                    ID = 0,
                    SkinName = skin,
                    SkinID = skinID < 0 ? KeepSkinID : (uint)skinID,
                    OverrideSpells = false,
                    ModelOnly = false,
                    ReplaceCharacterPackage = false
                });
            }
            Resolve();
            return true;
        }

        /// <summary>
        /// Pushes a model/skin override layer and emits S2C_ChangeCharacterData. Returns the layer id,
        /// which the caller passes to <see cref="PopSpecific"/> to revert exactly this layer.
        /// </summary>
        /// <param name="skinName">Internally named model/skin to display (e.g. "EliseSpider", "Ezreal_cyber_1").</param>
        /// <param name="skinID">Skin index, or -1 to keep the current skinID (wire sentinel).</param>
        /// <param name="overrideSpells">If true, the spellbook is taken from this layer (Nidalee/Elise).</param>
        /// <param name="modelOnly">If true, only the model changes (spellbook untouched).</param>
        /// <param name="replaceCharacterPackage">Replace the whole character package (rare).</param>
        public uint Push(string skinName, int skinID = -1, bool overrideSpells = false, bool modelOnly = false, bool replaceCharacterPackage = false)
        {
            uint id = _nextId++;
            var data = new CharacterStackData
            {
                ID = id,
                SkinName = skinName,
                SkinID = skinID < 0 ? KeepSkinID : (uint)skinID,
                OverrideSpells = overrideSpells,
                ModelOnly = modelOnly,
                ReplaceCharacterPackage = replaceCharacterPackage
            };
            _layers.Add(data);
            _game.PacketNotifier.NotifyS2C_ChangeCharacterData(_owner, data);
            Resolve();
            return id;
        }

        /// <summary>Removes the override layer with the given id (if present) and emits S2C_PopCharacterData.</summary>
        public void PopSpecific(uint id)
        {
            int i = _layers.FindIndex(d => d.ID == id);
            if (i < 0)
            {
                return;
            }
            _layers.RemoveAt(i);
            _game.PacketNotifier.NotifyS2C_PopCharacterData(_owner, id);
            Resolve();
        }

        /// <summary>Removes the topmost override layer (if any) and emits S2C_PopCharacterData.</summary>
        public void Pop()
        {
            if (_layers.Count == 0)
            {
                return;
            }
            uint id = _layers[^1].ID;
            _layers.RemoveAt(_layers.Count - 1);
            _game.PacketNotifier.NotifyS2C_PopCharacterData(_owner, id);
            Resolve();
        }

        /// <summary>Clears all override layers and emits S2C_PopAllCharacterData (revert to base).</summary>
        public void PopAll()
        {
            if (_layers.Count == 0)
            {
                return;
            }
            _layers.Clear();
            _game.PacketNotifier.NotifyS2C_PopAllCharacterData(_owner);
            Resolve();
        }

        /// <summary>
        /// Mirror of CharacterDataStack::Update: resolve the displayed model (top layer, else base)
        /// and sync it onto the owning unit for server-side logic. No packet is sent here — the
        /// Push/Pop/PopAll callers already emitted the authoritative wire packet.
        /// </summary>
        private void Resolve()
        {
            // Model + skinID come from the top layer (or base). modelOnly layers still count for the model.
            string top = BaseSkinName;
            uint skinID = BaseSkinID;
            if (_layers.Count > 0)
            {
                var back = _layers[^1];
                top = back.SkinName;
                if (back.SkinID != KeepSkinID)
                {
                    skinID = back.SkinID;
                }
            }
            if (top != _lastTop || skinID != _lastSkinID)
            {
                _lastTop = top;
                _lastSkinID = skinID;
                _owner.ApplyStackModel(top, skinID);
            }

            // Spellbook comes from the topmost overrideSpells layer (or the base character) — this is
            // independent of the model, so a modelOnly layer keeps the underlying spellbook.
            string spellSkin = GetSpellSkin();
            if (spellSkin != _lastSpellSkin)
            {
                _lastSpellSkin = spellSkin;
                _owner.ApplyStackSpellSkin(spellSkin);
            }
        }

        /// <summary>Mirror of CharacterDataStack::GetSpellSkin: topmost overrideSpells layer, else base character.</summary>
        private string GetSpellSkin()
        {
            for (int i = _layers.Count - 1; i >= 0; i--)
            {
                if (_layers[i].OverrideSpells)
                {
                    return _layers[i].SkinName;
                }
            }
            return _baseCharacterName;
        }
    }
}
