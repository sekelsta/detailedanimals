using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace DetailedAnimals {
    public class PlayerBondable : EntityBehavior {
        public static readonly int MAX_PLAYER_MEMORY = 8;
        private static readonly float MAX_DISTANT_FAMILIARITY = 20f;
        private static readonly float NEAR_DISTANCE = 20f;
        private static readonly float FAR_DISTANCE = 60f;
        private static readonly float FAMILIARITY_GAIN_RATE = 0.1f;
        private static readonly double FORGET_HOURS = 72;
        private static readonly float FORGET_RATE = FAMILIARITY_GAIN_RATE;
        private static readonly double SEARCH_FREQUENCY_HOURS = 0.25;

        private double nextSearchTime;
        private int verySlowTick;
        private EntityPartitioning partitionUtil;

        public PlayerBondable(Entity entity)
          : base(entity)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
            nextSearchTime = entity.Api.World.Calendar.TotalHours + entity.Api.World.Rand.NextDouble() * SEARCH_FREQUENCY_HOURS;
            verySlowTick = entity.Api.World.Rand.Next(1024);
        }

        public ITreeAttribute playerRelations
        {
            get
            {
                if (entity.WatchedAttributes.GetTreeAttribute("playerRelations") == null)
                {
                    entity.WatchedAttributes.SetAttribute("playerRelations", new TreeAttribute());
                }
                return entity.WatchedAttributes.GetTreeAttribute("playerRelations");
            }
            set
            {
                entity.WatchedAttributes.SetAttribute("playerRelations", value);
                entity.WatchedAttributes.MarkPathDirty("playerRelations");
            }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            // Read out initialization data from the Json
        }

        public ITreeAttribute RelationWith(IPlayer player) {
            return playerRelations.GetTreeAttribute(player.PlayerUID);
        }

        public void DropWeakestRelation() {
            Debug.Assert(playerRelations.Count > 0);
            string lowestId = null;
            float lowestFamiliarity = float.MaxValue;
            foreach (var pair in playerRelations) {
                float currentFamiliarity = (pair.Value as TreeAttribute).GetFloat("familiarity", 0);
                if (currentFamiliarity <= lowestFamiliarity) {
                    lowestId = pair.Key;
                    lowestFamiliarity = currentFamiliarity;
                }
            }
            playerRelations.RemoveAttribute(lowestId);
            entity.WatchedAttributes.MarkPathDirty("playerRelations");
        }

        public ITreeAttribute GetOrCreateRelation(IPlayer player) {
            string idString = player.PlayerUID;
            if (playerRelations.GetTreeAttribute(idString) == null) {
                if (playerRelations.Count >= MAX_PLAYER_MEMORY) {
                    DropWeakestRelation();
                }
                playerRelations.GetOrAddTreeAttribute(idString);
                entity.WatchedAttributes.MarkPathDirty("playerRelations");
            }
            return playerRelations.GetTreeAttribute(idString);
        }

        public float Familiarity(IPlayer player) {
            ITreeAttribute relation = RelationWith(player);
            if (relation == null) {
                return 0;
            }
            return relation.GetFloat("familiarity", 0);
        }

        public void Familiarity(IPlayer player, float val) {
            val = Math.Clamp(val, 0, 100);
            GetOrCreateRelation(player).SetFloat("familiarity", val);
            entity.WatchedAttributes.MarkPathDirty("playerRelations");
        }

        public void AddFamiliarity(IPlayer player, float val) {
            Familiarity(player, val + Familiarity(player));
        }

        public double LastSeen(IPlayer player) {
            ITreeAttribute relation = RelationWith(player);
            if (relation == null) {
                return 0;
            }
            return relation.GetFloat("lastseen", 0);
        }

        public void MarkSeen(IPlayer player) {
            GetOrCreateRelation(player).SetDouble("lastseen", entity.World.Calendar.TotalHours);
        }

        public float Opinion(IPlayer player) {
            ITreeAttribute relation = RelationWith(player);
            if (relation == null) {
                return 0;
            }
            return relation.GetFloat("opinion", 0);
        }

        public void Opinion(IPlayer player, float val) {
            val = Math.Clamp(val, -100, 100);
            GetOrCreateRelation(player).SetFloat("opinion", val);
            entity.WatchedAttributes.MarkPathDirty("playerRelations");
        }

        public void AddOpinion(IPlayer player, float val) {
            Opinion(player, val + Opinion(player));
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage) {
            base.OnEntityReceiveDamage(damageSource, ref damage);
            if (damage <= 0) {
                return;
            }
            Entity attacker = damageSource.GetCauseEntity();
            if (attacker is EntityPlayer) {
                AddOpinion((attacker as EntityPlayer).Player, -8 * damage);
            }
            // TODO: If being ridden, lower opinion of rider
        }

        public override void GetInfoText(StringBuilder infotext) {
            if (entity.World.Side == EnumAppSide.Client) {
                IPlayer player = (entity.World as IClientWorldAccessor).Player;
                int opinionInt = (int)Opinion(player);
                string opinionString = (opinionInt).ToString();
                if (opinionInt > 0) {
                    opinionString = "+" + opinionString;
                }
                if (player != null) {
                    string text = Lang.GetUnformatted("equine_adventures:infotext-bond")
                        .Replace("{playerName}", player.PlayerName)
                        .Replace("{familiarity}", ((int)Familiarity(player)).ToString())
                        .Replace("{opinion}", opinionString);
                    infotext.AppendLine(text);
                }
            }
            base.GetInfoText(infotext);
        }

        public override void OnGameTick(float deltaTime) {
            if (nextSearchTime < entity.Api.World.Calendar.TotalHours) {
                slowTick(nextSearchTime);
                nextSearchTime += SEARCH_FREQUENCY_HOURS;
            }
        }

        protected void slowTick(double updateTime) {
            verySlowTick += 1;
            float searchRadius = NEAR_DISTANCE;
            if (verySlowTick % 5 == 0) {
                searchRadius = FAR_DISTANCE;
            }
            partitionUtil.WalkInteractableEntities(entity.Pos.XYZ, searchRadius, (ActionConsumable<Entity>)(e => {
                    // TODO: Cancel if sleeping
                    if (!(e is EntityPlayer)) {
                        return true;
                    }
                    IPlayer player = (e as EntityPlayer).Player;
                    if (entity.ServerPos.SquareDistanceTo(e.ServerPos) > NEAR_DISTANCE * NEAR_DISTANCE 
                            && Familiarity(player) >= MAX_DISTANT_FAMILIARITY) {
                        return true;
                    }
                    AddFamiliarity(player, FAMILIARITY_GAIN_RATE);
                    return false;
                }
            ));
            if (verySlowTick % 8 == 0) {
                foreach (var pair in playerRelations) {
                    TreeAttribute tree = pair.Value as TreeAttribute;
                    double timeSinceSeen = updateTime - tree.GetFloat("lastseen", 0);
                    if (timeSinceSeen > FORGET_HOURS) {
                        float forgetAmount = FORGET_RATE;
                        if (timeSinceSeen > 2 * FORGET_HOURS) {
                            forgetAmount *= 2;
                        }
                        tree.SetFloat("familiarity", (float)Math.Max(0, tree.GetFloat("familiarity", 0) - forgetAmount));
                    }
                }
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemSlot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled) {
            if (byEntity is EntityPlayer) {
                IPlayer player = (byEntity as EntityPlayer).Player;
                // TODO: Gain familiarity depending on the item and stuff
                MarkSeen(player);
            }
            base.OnInteract(byEntity, itemSlot, hitPosition, mode, ref handled);
        }

        public override string PropertyName() => "playerbondable";
    }
}
