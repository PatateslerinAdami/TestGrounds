using System.Numerics;
using System.Linq;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public class Pet : Minion
    {
        private float _returnRadius;

        /// <summary>
        /// Entity that the pet is cloning (Ex. Who Mordekaiser's ghost is)
        /// </summary>
        public ObjAIBase ClonedUnit { get; private set; }
        /// <summary>
        /// Buff Assigned to this Pet at Spawn
        /// </summary>
        public Buff CloneBuff { get; }
        /// <summary>
        /// Spell that created this Pet
        /// </summary>
        public Spell SourceSpell { get; }
        /// <summary>
        /// Duration of CloneBuff
        /// </summary>
        public float LifeTime { get; }
        public bool CloneInventory { get; }
        public bool DoFade { get; }
        public bool ShowMinimapIconIfClone { get; }
        public bool DisallowPlayerControl { get; }
        public bool IsClone { get; }

        public Pet(
            Game game,
            Champion owner,
            Spell spell,
            Vector2 position,
            string name,
            string model,
            string buffName,
            float lifeTime,
            Stats stats = null,
            bool cloneInventory = true,
            bool showMinimapIfClone = true,
            bool disallowPlayerControl = false,
            bool doFade = false,
            bool isClone = true,
            string AIScript = "Pet"
            ) : base(game, owner, position, model, name, 0, owner.Team, owner.SkinID, stats: stats, AIScript: AIScript)
        {
            _returnRadius = GlobalData.ObjAIBaseVariables.DefaultPetReturnRadius;

            SourceSpell = spell;
            LifeTime = lifeTime;
            CloneBuff = new Buff(game, buffName, lifeTime, 1, spell, this, owner);
            CloneInventory = cloneInventory;
            ShowMinimapIconIfClone = showMinimapIfClone;
            DisallowPlayerControl = disallowPlayerControl;
            DoFade = doFade;
            IsClone = isClone;

            Owner.SetPet(this);
            game.ObjectManager.AddObject(this);
        }

        public Pet(
            Game game,
            Champion owner,
            Spell spell,
            ObjAIBase cloned,
            Vector2 position,
            string buffName,
            float lifeTime,
            Stats stats = null,
            bool cloneInventory = true,
            bool showMinimapIfClone = true,
            bool disallowPlayerControl = false,
            bool doFade = false,
            string AIScript = "Pet"
            ) : base(game, owner, cloned.Position, cloned.Model, cloned.Name, 0, owner.Team, cloned.SkinID, stats: stats, AIScript: AIScript)
        {

            if (position == Vector2.Zero)
            {
                Position = cloned.Position;
            }
            else
            {
                Position = position;
            }

            SourceSpell = spell;
            LifeTime = lifeTime;
            ClonedUnit = cloned;
            Stats.CurrentHealth = cloned.Stats.CurrentHealth;
            Stats.CurrentMana = cloned.Stats.CurrentMana;
            CloneBuff = new Buff(game, buffName, lifeTime, 1, spell, this, owner);
            CloneInventory = cloneInventory;
            ShowMinimapIconIfClone = showMinimapIfClone;
            DisallowPlayerControl = disallowPlayerControl;
            DoFade = doFade;
            IsClone = true;

            Owner.SetPet(this);
            game.ObjectManager.AddObject(this);
        }

        public override void OnAdded()
        {
            base.OnAdded();
            AddBuff(CloneBuff);

            if(CloneInventory && ClonedUnit != null)
            {
                foreach(var item in ClonedUnit.Inventory.GetAllItems())
                {
                    byte slot = ClonedUnit.Inventory.GetItemSlot(item);
                    var copied = Inventory.AddItemToSlot(item.ItemData, this, slot, item.GetState()).Key;
                    for (int stack = 1; stack < item.StackCount; stack++)
                    {
                        Inventory.AddItem(item.ItemData, this, item.GetState());
                    }
                    copied?.SetSpellCharges(item.SpellCharges);
                }
            }
        }

        public bool IssueCommand(GameServerCore.Enums.OrderType order, Vector2 destination, AttackableUnit target = null)
        {
            if (DisallowPlayerControl || Owner == null || IsDead || IsToRemove() || !CanIssueMoveOrders()) return false;
            if (!float.IsFinite(destination.X) || !float.IsFinite(destination.Y)) return false;

            if (order == GameServerCore.Enums.OrderType.PetHardStop)
            {
                SetTargetUnit(null, true);
                CancelAutoAttack(false, true);
                UpdateMoveOrder(order, true);
                StopMovement();
                return true;
            }

            if (order == GameServerCore.Enums.OrderType.PetHardAttack)
            {
                if (target == null || target.IsDead || target.IsToRemove() || target.Team == Team
                    || !target.GetIsTargetableToTeam(Team)
                    || !target.Status.HasFlag(GameServerCore.Enums.StatusFlags.Targetable)
                    || !_game.ObjectManager.TeamHasVisionOn(Team, target)) return false;
                if (TargetUnit == target)
                {
                    UpdateMoveOrder(order, true);
                    return true;
                }
                destination = target.Position;
            }
            else if (order == GameServerCore.Enums.OrderType.PetHardReturn)
            {
                target = null;
                destination = Owner.Position;
            }
            else if (order == GameServerCore.Enums.OrderType.PetHardMove)
            {
                target = null;
            }
            else return false;

            destination = _game.Map.NavigationGrid.GetClosestTerrainExit(destination, PathfindingRadius);
            var path = _game.Map.PathingHandler.GetPath(Position, destination, PathfindingRadius);
            if (path == null || path.Count == 0) return false;

            CancelAutoAttack(false, true);
            SetTargetUnit(target, true);
            SetWaypoints(path);
            UpdateMoveOrder(order, true);
            return true;
        }

        public override void OnRemoved()
        {
            foreach (var item in Inventory.GetAllItems())
            {
                Inventory.RemoveItem(Inventory.GetItemSlot(item), this, item.StackCount);
            }
            foreach (var buff in GetBuffs().ToArray()) RemoveBuff(buff);
            LeagueSandbox.GameServer.API.ApiEventManager.RemoveAllListenersForOwner(AIScript);
            if (Owner.GetPet() == null || Owner.GetPet() == this) Owner.SetPet(null);
            base.OnRemoved();
        }

        public float GetReturnRadius()
        {
            return _returnRadius;
        }

        public void SetReturnRadius(float radius)
        {
            _returnRadius = radius;
        }
    }
}
