using System;
using System.Collections.Generic;
using ArcaneCode.Core;
using UnityEngine;

namespace ArcaneCode
{
    public sealed partial class ArcaneGame
    {
        sealed class Enemy
        {
            public Transform View;
            public ActorArt Art;
            public Vector2 Position;
            public EnemyDefinition Definition;
            public float HP, MaxHP, Cooldown, Slow, Freeze, Burn, BurnTick, Warning;
            public Color BaseColor;
            public bool Archer, Boss;
            public Vector2 Aim;
            public SpriteRenderer Telegraph;
        }
        enum EnemyKind { Skeleton, Bat, Goblin, Rat, MiniMage, Spider, DarkKnight, HeadlessKnight, BasiliskFrog }
        sealed class EnemyTemplate
        {
            public EnemyDefinition Definition;
            public string SpriteId;
            public Color Color;
            public bool Ranged;
        }
        sealed class Shot
        {
            public Transform View;
            public WorldPropArt Art;
            public Vector2 Position, Velocity;
            public SpellDefinition Spell;
            public float Damage, Life = 4, Radius;
            public bool Hostile;
            public int Energy, Ricochets;
        }
        sealed class Orb { public Transform View; public WorldPropArt Art; public Vector2 Position; public int Experience, Coins; }
        sealed class ItemPickup { public Transform View; public WorldPropArt Art; public Vector2 Position; public RunItemInstance Item; }
        sealed class Chest { public Transform View; public Vector2 Position; }
        sealed class ShopOffer { public Transform View; public WorldPropArt Art; public Vector2 Position; public RunItemInstance Item; public Reward Reward; public int Price; public bool Sold; }
        sealed class Effect { public Transform View; public WorldPropArt Art; public float Life, Duration, Radius; }
        sealed class Door { public int Target; public Vector2 Direction; public SpriteRenderer Symbol; }
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Orb> orbs = new List<Orb>();
        readonly List<ItemPickup> itemPickups = new List<ItemPickup>();
        readonly List<ShopOffer> shopOffers = new List<ShopOffer>();
        Chest chest;
        readonly List<Effect> effects = new List<Effect>();
        readonly List<Door> doors = new List<Door>();
        readonly int[,] flow = new int[33,17];
        readonly Queue<Vector2Int> flowQueue = new Queue<Vector2Int>();
        static readonly Vector2Int[] FlowDirections = { Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right };
        float flowTimer;

        void BuildRoomArt(int template, bool hub = false)
        {
            if (roomRoot != null) Destroy(roomRoot.gameObject);
            roomRoot = new GameObject("Sala").transform;
            enemies.Clear(); shots.Clear(); orbs.Clear(); itemPickups.Clear(); chest=null; effects.Clear(); obstacles.Clear(); doors.Clear();
            for (int x = -9; x < 9; x++) for (int y = -5; y < 5; y++)
                WorldArt.Draw(roomRoot,"floor",new Vector2(x+.5f,y+.5f),Vector2.one, ((x+y)&1)==0 ? Color.white : new Color(.9f,.91f,.96f),-1000);
            Color wall = new Color(.22f,.26f,.36f);
            WorldArt.Draw(roomRoot,"square",new Vector2(0,5.15f),new Vector2(19,1),wall,-400);
            WorldArt.Draw(roomRoot,"square",new Vector2(0,-5.15f),new Vector2(19,.8f),wall,500);
            WorldArt.Draw(roomRoot,"square",new Vector2(-9,0),new Vector2(1,10),wall,250);
            WorldArt.Draw(roomRoot,"square",new Vector2(9,0),new Vector2(1,10),wall,250);
            WorldArt.Draw(roomRoot,"square",new Vector2(0,4.72f),new Vector2(18,.08f),new Color(.47f,.46f,.61f),-399);
            WorldArt.Draw(roomRoot,"ring",Vector2.zero,new Vector2(4,4),new Color(.24f,.32f,.45f,.38f),-800,false);
            WorldArt.Draw(roomRoot,"diamond",Vector2.zero,Vector2.one*.9f,new Color(.24f,.32f,.45f,.3f),-799,false);
            if (!hub && (dungeon == null || CurrentRoom.Kind != RoomKind.Boss))
            {
                Vector2[][] layouts = {
                    new[] {new Vector2(-3,2),new Vector2(3,2),new Vector2(-3,-2),new Vector2(3,-2)},
                    new[] {new Vector2(-3,0),new Vector2(3,0),new Vector2(-5,2.5f),new Vector2(5,-2.5f)},
                    new[] {new Vector2(-4,1.6f),new Vector2(4,1.6f),new Vector2(0,-2.3f)}
                };
                foreach (Vector2 at in layouts[template % layouts.Length])
                {
                    obstacles.Add(new Rect(at.x-.48f,at.y-.35f,.96f,.7f));
                    WorldArt.Draw(roomRoot,"disc",at+new Vector2(.15f,-.1f),new Vector2(1.4f,.5f),new Color(0,0,0,.4f),90-Mathf.RoundToInt(at.y*15),false);
                    WorldArt.Draw(roomRoot,"pillar",at+new Vector2(0,.55f),new Vector2(1.25f,1.7f),Color.white,100-Mathf.RoundToInt(at.y*15));
                }
            }
            foreach (float x in new[] {-7f,7f}) foreach (float y in new[] {-3.8f,3.8f})
            {
                WorldArt.Draw(roomRoot,"diamond",new Vector2(x,y),Vector2.one*.2f,Accent,200,false);
                WorldArt.Lamp(roomRoot,new Vector2(x,y),Accent,3.5f,.8f);
            }
        }
        void EnterRoom(int index, Vector2 entry)
        {
            roomIndex = index; CurrentRoom.Visited = true; energy = 0; invincible = 1; transition = .7f;
            BuildRoomArt(CurrentRoom.Template); SpawnPlayer(entry); machine = CreateMachine(); flowTimer = 0;
            foreach (int next in CurrentRoom.Neighbors)
            {
                Vector2 direction = new Vector2(dungeon.Rooms[next].X-CurrentRoom.X,dungeon.Rooms[next].Y-CurrentRoom.Y);
                Vector2 position = Vector2.Scale(direction,new Vector2(8.6f,4.8f));
                Vector2 scale = direction.x == 0 ? new Vector2(1.7f,.9f) : new Vector2(.9f,1.7f);
                WorldArt.Draw(roomRoot,"square",position,scale,new Color(.035f,.04f,.07f),501,false);
                var symbol = WorldArt.Draw(roomRoot,"diamond",position,Vector2.one*.35f,Accent,502,false);
                doors.Add(new Door { Target=next,Direction=direction,Symbol=symbol });
            }
            if (!CurrentRoom.Cleared)
            {
                if (CurrentRoom.Kind == RoomKind.Boss) SpawnEnemy(new Vector2(0,2),SuperBossTemplate(),true);
                else
                {
                    int floor=CurrentRoom.Floor;
                    EnemyTemplate[] roster=EnemyRoster(floor);
                    int count = 5 + Mathf.Min(3,floor);
                    int rosterOffset=random.Next(roster.Length);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 position = Vector2.zero;
                        for (int attempt = 0; attempt < 150; attempt++)
                        {
                            position = new Vector2((float)random.NextDouble()*13-6.5f,(float)random.NextDouble()*6-3);
                            if (Walkable(position,.45f) && Vector2.Distance(position,entry)>3 && SpawnPositionIsFree(position)) break;
                        }
                        SpawnEnemy(position,roster[(i+rosterOffset)%roster.Length],false);
                    }
                }
            }
            RefreshDoors();
            if (CurrentRoom.Kind == RoomKind.Rest && !CurrentRoom.Claimed)
            { CurrentRoom.Claimed = true; hp = Mathf.Min(MaxHealth,hp+MaxHealth*.4f); Notify("Santuário · 40% da vida máxima recuperada."); }
            if (CurrentRoom.Kind == RoomKind.Shop)
            {
                Tutorial("shop","A loja é uma sala física: aproxime-se de uma oferta e aperte E para comprar. Cartas custam moedas e aplicam uma melhoria aleatória.");
                SpawnShopOffers();
            }
            RestorePickups();
            if (CurrentRoom.Kind == RoomKind.Treasure && !CurrentRoom.ChestOpened) SpawnChest();
        }

        void RestorePickups()
        {
            if (CurrentRoom.UncollectedExperience > 0) SpawnExperienceOrb(Vector2.zero, CurrentRoom.UncollectedExperience);
            if (CurrentRoom.UncollectedCoins > 0) SpawnCoinOrb(new Vector2(.5f,0), CurrentRoom.UncollectedCoins);
            foreach (RunItemInstance item in CurrentRoom.UncollectedItems) SpawnItemPickup(item, new Vector2(0,-.35f));
        }
        void SpawnChest()
        {
            Vector2 at=Vector2.zero;
            chest=new Chest { Position=at,View=WorldProps.Chest(roomRoot,at).transform };
        }
        void SpawnItemPickup(RunItemInstance item, Vector2 position)
        {
            if (!RunItemCatalog.TryGet(item.DefinitionId,out RunItemDefinition definition)) return;
            var pickup=new ItemPickup { Item=item,Position=position };
            pickup.Art=WorldProps.Item(roomRoot,position,definition.Kind,item.DefinitionId.Contains("ice") || item.DefinitionId.Contains("frost"));
            pickup.View=pickup.Art.transform;
            itemPickups.Add(pickup);
        }
        RunItemInstance CreateChestDrop()
        {
            var pool=new List<RunItemDefinition>();
            pool.AddRange(RunItemCatalog.OfKind(RunItemKind.Ring));
            pool.AddRange(RunItemCatalog.OfKind(RunItemKind.Staff));
            pool.AddRange(RunItemCatalog.OfKind(RunItemKind.Grimoire));
            RunItemDefinition definition=pool[random.Next(pool.Count)];
            int max=Mathf.Clamp(CurrentRoom.Floor,1,definition.MaxLevel);
            return new RunItemInstance { DefinitionId=definition.Id,Level=random.Next(1,max+1) };
        }
        void SpawnShopOffers()
        {
            if (!CurrentRoom.Claimed)
            {
                CurrentRoom.Claimed=true; shopOffers.Clear();
                RunItemInstance first=CreateChestDrop(), second=CreateChestDrop();
                List<Reward> cards=RewardPool(); Reward card=cards[random.Next(cards.Count)];
                shopOffers.Add(new ShopOffer { Position=new Vector2(-3,1),Item=first,Price=10+first.Level*6 });
                shopOffers.Add(new ShopOffer { Position=new Vector2(0,1),Item=second,Price=10+second.Level*6 });
                shopOffers.Add(new ShopOffer { Position=new Vector2(3,1),Reward=card,Price=ShopPrice(card) });
            }
            foreach (ShopOffer offer in shopOffers) if (!offer.Sold) SpawnShopOffer(offer);
            Notify("Loja arcana · interaja com uma oferta para comprar.");
        }
        void SpawnShopOffer(ShopOffer offer)
        {
            if (offer.Reward!=null) offer.Art=WorldProps.Card(roomRoot,offer.Position);
            else if (offer.Item!=null && RunItemCatalog.TryGet(offer.Item.DefinitionId,out RunItemDefinition definition))
                offer.Art=WorldProps.Item(roomRoot,offer.Position,definition.Kind,offer.Item.DefinitionId.Contains("ice") || offer.Item.DefinitionId.Contains("frost"));
            else return;
            offer.View=offer.Art.transform;
        }
        bool TryBuyShopOffer()
        {
            ShopOffer closest=null; float distance=.9f;
            foreach (ShopOffer offer in shopOffers)
            {
                if (offer.Sold || offer.View == null) continue;
                float candidate=Vector2.Distance(playerPosition,offer.Position); if (candidate<distance) { distance=candidate; closest=offer; }
            }
            if (closest == null) return false;
            if (runCoins<closest.Price) { Notify("São necessárias "+closest.Price+" moedas."); return true; }
            runCoins-=closest.Price; closest.Sold=true;
            if (closest.Item != null)
            {
                inventory.Add(closest.Item);
                if (RunItemCatalog.TryGet(closest.Item.DefinitionId,out RunItemDefinition definition)) Notify(definition.Label+" comprado.");
            }
            else { ApplyReward(closest.Reward); Notify("Carta: "+closest.Reward.Title); }
            Destroy(closest.View.gameObject); return true;
        }
        bool TryInteract()
        {
            if (chest != null && Vector2.Distance(playerPosition,chest.Position) < 1.05f)
            {
                Tutorial("chest","Chegue perto do baú e aperte E para abri-lo; depois, aperte E perto do item para guardá-lo na mochila.");
                CurrentRoom.ChestOpened=true; Destroy(chest.View.gameObject); chest=null;
                RunItemInstance item=CreateChestDrop(); CurrentRoom.UncollectedItems.Add(item); SpawnItemPickup(item,new Vector2(0,.55f));
                Notify("Baú aberto · interaja com o item para guardá-lo."); return true;
            }
            ItemPickup closest=null; float distance=.85f;
            foreach (ItemPickup pickup in itemPickups)
            { float candidate=Vector2.Distance(playerPosition,pickup.Position); if (candidate<distance) { distance=candidate; closest=pickup; } }
            if (closest == null || !inventory.Add(closest.Item)) return false;
            CurrentRoom.UncollectedItems.RemoveAll(item=>item.InstanceId==closest.Item.InstanceId);
            if (RunItemCatalog.TryGet(closest.Item.DefinitionId,out RunItemDefinition definition)) Notify(definition.Label+" nível "+closest.Item.Level+" guardado.");
            Destroy(closest.View.gameObject); itemPickups.Remove(closest); return true;
        }
        void AbsorbRoomResources()
        {
            int experience=CurrentRoom.UncollectedExperience, coins=CurrentRoom.UncollectedCoins;
            CurrentRoom.UncollectedExperience=0; CurrentRoom.UncollectedCoins=0;
            if (experience>0) GainXP(experience);
            if (coins>0) runCoins+=coins;
            foreach (Orb orb in orbs) if (orb.View!=null) Destroy(orb.View.gameObject);
            orbs.Clear();
            if (experience>0 || coins>0) Notify("Sala limpa · +"+experience+" XP · +"+coins+" moedas");
        }

        void SpawnExperienceOrb(Vector2 position, int amount)
        {
            var orb = new Orb { Position = position, Experience = amount };
            orb.Art=WorldProps.Resource(roomRoot,position,false); orb.View=orb.Art.transform;
            orbs.Add(orb);
        }
        void SpawnCoinOrb(Vector2 position, int amount)
        {
            var orb = new Orb { Position = position, Coins = amount };
            orb.Art=WorldProps.Resource(roomRoot,position,true); orb.View=orb.Art.transform;
            orbs.Add(orb);
        }
        void RefreshDoors()
        { foreach (Door door in doors) door.Symbol.color = CurrentRoom.Cleared ? dungeon.Rooms[door.Target].Kind == RoomKind.Boss ? new Color(1,.3f,.35f) : Accent : new Color(.65f,.2f,.25f); }
        void CheckDoors()
        {
            foreach (Door door in doors)
            {
                if (door.Direction.x != 0 && playerPosition.x*door.Direction.x>7.95f && Mathf.Abs(playerPosition.y)<.75f ||
                    door.Direction.y != 0 && playerPosition.y*door.Direction.y>4.02f && Mathf.Abs(playerPosition.x)<.75f)
                { EnterRoom(door.Target,-Vector2.Scale(door.Direction,new Vector2(7.3f,3.5f))); return; }
            }
        }
        bool Walkable(Vector2 p, float radius)
        {
            if (Mathf.Abs(p.x)>8.55f-radius || Mathf.Abs(p.y)>4.55f-radius) return false;
            foreach (Rect r in obstacles)
            { Vector2 closest = new Vector2(Mathf.Clamp(p.x,r.xMin,r.xMax),Mathf.Clamp(p.y,r.yMin,r.yMax)); if ((p-closest).sqrMagnitude < radius*radius) return false; }
            return true;
        }
        Vector2 Move(Vector2 position, Vector2 delta, float radius)
        {
            int steps = Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.15f)); delta /= steps;
            for (int i=0;i<steps;i++)
            {
                Vector2 x = position+new Vector2(delta.x,0); if (Walkable(x,radius)) position=x;
                Vector2 y = position+new Vector2(0,delta.y); if (Walkable(y,radius)) position=y;
            }
            return position;
        }
        bool Visible(Vector2 a, Vector2 b)
        {
            int count = Mathf.CeilToInt(Vector2.Distance(a,b)/.12f);
            for (int i=1;i<count;i++)
            {
                Vector2 p=Vector2.Lerp(a,b,(float)i/count);
                for (int obstacle=0;obstacle<obstacles.Count;obstacle++) if (obstacles[obstacle].Contains(p)) return false;
            }
            return true;
        }
        bool SpawnPositionIsFree(Vector2 position)
        {
            for (int i=0;i<enemies.Count;i++) if (Vector2.Distance(position,enemies[i].Position)<=1) return false;
            return true;
        }
        void RebuildFlow()
        {
            for (int x=0;x<33;x++) for (int y=0;y<17;y++) flow[x,y]=-1;
            flowQueue.Clear(); Vector2Int start = Cell(playerPosition); flow[start.x,start.y]=0; flowQueue.Enqueue(start);
            while (flowQueue.Count>0)
            {
                Vector2Int cell = flowQueue.Dequeue();
                for (int direction=0;direction<FlowDirections.Length;direction++)
                {
                    Vector2Int dir=FlowDirections[direction];
                    Vector2Int n = cell+dir;
                    if (n.x<0 || n.x>=33 || n.y<0 || n.y>=17 || flow[n.x,n.y]!=-1 || !Walkable(CellPosition(n),.31f)) continue;
                    flow[n.x,n.y]=flow[cell.x,cell.y]+1; flowQueue.Enqueue(n);
                }
            }
        }
        Vector2Int Cell(Vector2 at) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((at.x+8)*2),0,32),Mathf.Clamp(Mathf.RoundToInt((at.y+4)*2),0,16));
        Vector2 CellPosition(Vector2Int at) => new Vector2(at.x*.5f-8,at.y*.5f-4);
        Vector2 Chase(Enemy enemy)
        {
            if (Visible(enemy.Position,playerPosition)) return (playerPosition-enemy.Position).normalized;
            Vector2Int current = Cell(enemy.Position), best = current; int score = int.MaxValue;
            for (int x=-1;x<=1;x++) for (int y=-1;y<=1;y++)
            {
                Vector2Int next = current+new Vector2Int(x,y);
                if (next.x<0 || next.x>=33 || next.y<0 || next.y>=17) continue;
                int distance = flow[next.x,next.y];
                if (distance>=0 && distance<score && Visible(enemy.Position,CellPosition(next))) { score=distance; best=next; }
            }
            return (CellPosition(best)-enemy.Position).normalized;
        }
        EnemyTemplate[] EnemyRoster(int floor)
        {
            bool undead = floor == 1 ? dungeon.UndeadFirst : !dungeon.UndeadFirst;
            if (floor == 1 || floor == 2) return undead
                ? new[] { Template(EnemyKind.Skeleton), Template(EnemyKind.Bat) }
                : new[] { Template(EnemyKind.Goblin), Template(EnemyKind.Rat) };
            if (floor == 3) return new[] { Template(EnemyKind.MiniMage), Template(EnemyKind.Spider) };
            return new[] { Template(EnemyKind.DarkKnight), Template(EnemyKind.HeadlessKnight), Template(EnemyKind.BasiliskFrog) };
        }
        EnemyTemplate Template(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Bat: return new EnemyTemplate { Definition=config.Bat, SpriteId="bat", Color=new Color(.42f,.34f,.58f) };
                case EnemyKind.Goblin: return new EnemyTemplate { Definition=config.Goblin, SpriteId="goblin", Color=new Color(.38f,.72f,.34f) };
                case EnemyKind.Rat: return new EnemyTemplate { Definition=config.Rat, SpriteId="rat", Color=new Color(.54f,.48f,.42f) };
                case EnemyKind.MiniMage: return new EnemyTemplate { Definition=config.MiniMage, SpriteId="mini-mage", Color=new Color(.68f,.42f,.92f), Ranged=true };
                case EnemyKind.Spider: return new EnemyTemplate { Definition=config.Spider, SpriteId="spider", Color=new Color(.22f,.18f,.29f) };
                case EnemyKind.DarkKnight: return new EnemyTemplate { Definition=config.DarkKnight, SpriteId="black-knight", Color=new Color(.25f,.29f,.36f) };
                case EnemyKind.HeadlessKnight: return new EnemyTemplate { Definition=config.HeadlessKnight, SpriteId="flame-headless-knight", Color=new Color(1,.36f,.16f) };
                case EnemyKind.BasiliskFrog: return new EnemyTemplate { Definition=config.BasiliskFrog, SpriteId="basilisk-frog", Color=new Color(.62f,.78f,.18f), Ranged=true };
                default: return new EnemyTemplate { Definition=config.Melee, SpriteId="skeleton", Color=Color.white };
            }
        }
        EnemyTemplate SuperBossTemplate()
        {
            EnemyDefinition baseDefinition=config.DarkKnight;
            return new EnemyTemplate
            {
                Definition=new EnemyDefinition { Label="Cavaleiro Negro Supremo", Health=baseDefinition.Health*4.5f, Speed=baseDefinition.Speed*.82f, Damage=baseDefinition.Damage*1.45f, AttackInterval=baseDefinition.AttackInterval*.78f, Experience=baseDefinition.Experience*5, Coins=baseDefinition.Coins*2 },
                SpriteId="black-knight", Color=new Color(.9f,.18f,.24f)
            };
        }
        void SpawnEnemy(Vector2 position, EnemyTemplate template, bool boss)
        {
            EnemyDefinition definition=template.Definition;
            bool hasSprite=EnemySprites.Available(template.SpriteId);
            Color color=hasSprite ? Color.white : template.Color;
            var enemy = new Enemy { Position=position,Definition=definition,HP=definition.Health,MaxHP=definition.Health,BaseColor=color,Archer=template.Ranged,Boss=boss,Cooldown=1+(float)random.NextDouble() };
            enemy.Art=WorldArt.Actor(roomRoot,boss?"boss":"skeleton",position,color,boss?2:1.1f,hasSprite ? direction=>EnemySprites.Facing(template.SpriteId,direction) : null); enemy.View=enemy.Art.transform; enemies.Add(enemy);
        }
        Enemy FindTarget()
        {
            Enemy target=null; float distance=100;
            foreach (var enemy in enemies)
            { float d=(enemy.Position-playerPosition).sqrMagnitude; if (d<distance && Visible(playerPosition,enemy.Position)) { distance=d; target=enemy; } }
            return target;
        }
        public bool Cast(string spellId) => Cast(spellId,1);
        public bool Cast(string spellId, float speedMultiplier)
        {
            float speed=Mathf.Max(1,speedMultiplier);
            if ((mode!=ScreenMode.Run && mode!=ScreenMode.Inventory) || elapsed-lastCast<config.SpellInterval/speed || playerArt.Casting || !unlocked.Contains(spellId)) return false;
            SpellDefinition definition=SpellFor(spellId); Enemy target=FindTarget();
            if (definition==null || target==null) return false;
            int charged=Mathf.Max(1,energy); float damage=definition.Damage*(1+damageBonus)*(1+charged*.25f)*loadout.DamageMultiplierFor(spellId);
            energy=0; lastCast=elapsed;
            Vector2 direction=(target.Position-playerPosition).normalized;
            Vector2 castOrigin=playerPosition+direction*.52f;
            playerArt.Cast(direction);
            Burst(castOrigin,.32f,definition.Color,.14f);
            if (definition.Area)
            {
                Burst(playerPosition,definition.Radius,definition.Color,.45f);
                for (int i=enemies.Count-1;i>=0;i--)
                {
                    Enemy enemy=enemies[i];
                    if (Vector2.Distance(playerPosition,enemy.Position)<=definition.Radius && Visible(playerPosition,enemy.Position)) Hit(enemy,damage,definition,charged);
                }
            }
            else
            {
                var shot=new Shot { Position=castOrigin,Velocity=direction*definition.Speed,Spell=definition,Damage=damage,Radius=definition.Radius,Energy=charged,Ricochets=inventory.RicochetCount };
                shot.Art=WorldProps.Projectile(roomRoot,shot.Position,definition.Radius*2+.05f*charged,definition.Color,definition.Ice,direction);
                shot.View=shot.Art.transform; shots.Add(shot);
            }
            return true;
        }
        SpellDefinition SpellFor(string id)
        {
            for (int i=0;i<config.Spells.Count;i++) if (config.Spells[i].Id==id) return config.Spells[i];
            return null;
        }
        void Hit(Enemy enemy, float damage, SpellDefinition spell, int charge)
        {
            if (!enemies.Contains(enemy)) return;
            enemy.HP-=damage;
            if (spell!=null)
            {
                if (spell.Ice) { enemy.Slow=2.2f; if (charge>=3) enemy.Freeze=enemy.Boss?.65f:1.4f; }
                else { enemy.Burn=2.5f; enemy.BurnTick=.5f; }
                Burst(enemy.Position,.5f,spell.Color,.18f);
            }
            if (enemy.HP<=0) Kill(enemy);
        }
        bool RedirectRicochet(Shot shot, Enemy hit)
        {
            if (shot.Ricochets <= 0) return false;
            Enemy target=null; float distance=float.MaxValue;
            for (int i=0;i<enemies.Count;i++)
            {
                Enemy candidate=enemies[i];
                if (candidate==hit || !Visible(hit.Position,candidate.Position)) continue;
                float candidateDistance=(candidate.Position-hit.Position).sqrMagnitude;
                if (candidateDistance<distance) { distance=candidateDistance; target=candidate; }
            }
            if (target==null) return false;
            shot.Ricochets--; shot.Position=hit.Position;
            shot.Velocity=(target.Position-hit.Position).normalized*shot.Velocity.magnitude;
            Burst(hit.Position,.32f,shot.Spell.Color,.12f);
            return true;
        }
        void Kill(Enemy enemy)
        {
            Tutorial("first-kill","DICA DE CÓDIGO · `this.inimigos` informa quantos inimigos ainda restam na sala. Consulte esse valor no seu código para adaptar sua estratégia durante a luta.");
            enemies.Remove(enemy); Destroy(enemy.View.gameObject); if (enemy.Telegraph!=null) Destroy(enemy.Telegraph.gameObject);
            CurrentRoom.UncollectedExperience += enemy.Definition.Experience;
            int coinDrop = enemy.Boss ? 10 : enemy.Definition.Coins;
            CurrentRoom.UncollectedCoins += coinDrop;
            SpawnExperienceOrb(enemy.Position, enemy.Definition.Experience);
            SpawnCoinOrb(enemy.Position+new Vector2(.2f,0), coinDrop);
        }
        void DamagePlayer(float damage)
        { if (invincible>0 || hp<=0) return; hp=Mathf.Max(0,hp-damage); invincible=config.Invulnerability; Burst(playerPosition,.65f,new Color(1,.27f,.35f),.25f); }
        void Burst(Vector2 at,float radius,Color color,float duration)
        {
            WorldPropArt art=WorldProps.Burst(roomRoot,at,color); art.transform.localScale=Vector3.one*.1f;
            effects.Add(new Effect { View=art.transform,Art=art,Life=duration,Duration=duration,Radius=radius });
        }
        void EnemyShot(Vector2 from,Vector2 direction,float damage,float speed=4.5f)
        {
            var shot=new Shot { Position=from,Velocity=direction.normalized*speed,Damage=damage,Radius=.16f,Hostile=true };
            shot.Art=WorldProps.Projectile(roomRoot,from,.3f,new Color(1,.34f,.48f),true,direction);
            shot.View=shot.Art.transform; shots.Add(shot);
        }
        void UpdateCombat(float dt)
        {
            foreach (ItemPickup pickup in itemPickups) if (pickup.Art!=null) pickup.Art.Animate(dt);
            foreach (ShopOffer offer in shopOffers) if (!offer.Sold && offer.Art!=null) offer.Art.Animate(dt);
            flowTimer-=dt; if (flowTimer<=0) { RebuildFlow(); flowTimer=.25f; }
            for (int enemyIndex=enemies.Count-1;enemyIndex>=0;enemyIndex--)
            {
                Enemy enemy=enemies[enemyIndex];
                enemy.Slow-=dt; enemy.Freeze-=dt;
                if (enemy.Burn>0)
                {
                    enemy.Burn-=dt; enemy.BurnTick-=dt;
                    if (enemy.BurnTick<=0) { enemy.BurnTick=.5f; enemy.HP-=3; if (enemy.HP<=0) { Kill(enemy); continue; } }
                }
                SpriteRenderer body=enemy.Art.Body;
                body.color=enemy.Freeze>0?new Color(.45f,.85f,1):enemy.Slow>0?new Color(.7f,.9f,1):enemy.Burn>0?new Color(1,.7f,.45f):enemy.BaseColor;
                if (enemy.Freeze>0) continue;
                enemy.Cooldown-=dt;
                float distance=Vector2.Distance(enemy.Position,playerPosition);
                if (enemy.Warning>0)
                {
                    enemy.Warning-=dt;
                    if (enemy.Telegraph!=null) enemy.Telegraph.color=new Color(1,.25f,.35f,.35f+Mathf.PingPong(elapsed*3,.4f));
                    if (enemy.Warning<=0)
                    {
                        if (enemy.Telegraph!=null) Destroy(enemy.Telegraph.gameObject);
                        if (enemy.Boss)
                        {
                            for (int i=0;i<12;i++) { float angle=i*Mathf.PI/6+elapsed*.3f; EnemyShot(enemy.Position,new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),enemy.Definition.Damage,3.5f); }
                            Burst(enemy.Position,2.2f,new Color(1,.3f,.4f),.35f); if (distance<2.2f) DamagePlayer(enemy.Definition.Damage);
                        }
                        else EnemyShot(enemy.Position,enemy.Aim-enemy.Position,enemy.Definition.Damage);
                    }
                    continue;
                }
                Vector2 movement=Chase(enemy);
                if (enemy.Archer && distance<5 && Visible(enemy.Position,playerPosition)) movement=distance<2.5f?-movement:Vector2.zero;
                if (enemy.Boss && distance<2) movement=Vector2.zero;
                foreach (var other in enemies) if (other!=enemy)
                { Vector2 away=enemy.Position-other.Position; if (away.sqrMagnitude<.5f && away.sqrMagnitude>.001f) movement+=away.normalized*.55f; }
                enemy.Position=Move(enemy.Position,movement.normalized*(enemy.Definition.Speed*(enemy.Slow>0?.5f:1)*dt),enemy.Boss?.48f:.3f);
                enemy.View.position=enemy.Position; WorldArt.Sort(enemy.Art,elapsed*9,movement.sqrMagnitude>0);
                if (distance<(enemy.Boss?.9f:.62f)) DamagePlayer(enemy.Definition.Damage);
                if (enemy.Cooldown<=0 && (enemy.Boss || enemy.Archer && Visible(enemy.Position,playerPosition)))
                {
                    enemy.Cooldown=enemy.Definition.AttackInterval; enemy.Warning=enemy.Boss?1:.6f; enemy.Aim=playerPosition;
                    if (enemy.Boss) enemy.Telegraph=WorldArt.Draw(roomRoot,"ring",enemy.Position,Vector2.one*4.4f,new Color(1,.3f,.35f,.7f),5,false);
                    else
                    {
                        Vector2 delta=enemy.Aim-enemy.Position;
                        enemy.Telegraph=WorldArt.Draw(roomRoot,"square",enemy.Position+delta*.5f,new Vector2(delta.magnitude,.035f),new Color(1,.3f,.35f,.6f),5,false);
                        enemy.Telegraph.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                    }
                }
            }
            for (int i=shots.Count-1;i>=0;i--)
            {
                Shot shot=shots[i]; shot.Life-=dt; bool hit=false;
                int substeps=Mathf.Max(1,Mathf.CeilToInt(shot.Velocity.magnitude*dt/.1f));
                for (int step=0;step<substeps && !hit;step++)
                {
                    shot.Position+=shot.Velocity*dt/substeps;
                    if (!Walkable(shot.Position,shot.Radius*.5f)) { hit=true; break; }
                    if (shot.Hostile)
                    { if (Vector2.Distance(shot.Position,playerPosition)<shot.Radius+.27f) { DamagePlayer(shot.Damage); hit=true; } }
                    else for (int enemyIndex=enemies.Count-1;enemyIndex>=0;enemyIndex--)
                    {
                        Enemy enemy=enemies[enemyIndex];
                        if (Vector2.Distance(shot.Position,enemy.Position)<shot.Radius+(enemy.Boss?.65f:.32f)) { Hit(enemy,shot.Damage,shot.Spell,shot.Energy); hit=!RedirectRicochet(shot,enemy); break; }
                    }
                }
                if (hit || shot.Life<=0) { Destroy(shot.View.gameObject); shots.RemoveAt(i); }
                else { shot.View.position=shot.Position; shot.Art.Aim(shot.Velocity); shot.Art.Animate(dt); shot.Art.SetOrder(350); }
            }
            for (int i=orbs.Count-1;i>=0;i--)
            {
                Orb orb=orbs[i]; float distance=Vector2.Distance(orb.Position,playerPosition);
                if (distance<2.2f) orb.Position=Vector2.MoveTowards(orb.Position,playerPosition,7*dt);
                orb.View.position=orb.Position; orb.Art.Animate(dt);
                if (distance<.4f)
                {
                    if (orb.Experience > 0)
                    {
                        CurrentRoom.UncollectedExperience = Mathf.Max(0, CurrentRoom.UncollectedExperience-orb.Experience);
                        GainXP(orb.Experience);
                    }
                    if (orb.Coins > 0)
                    {
                        CurrentRoom.UncollectedCoins = Mathf.Max(0, CurrentRoom.UncollectedCoins-orb.Coins);
                        runCoins += orb.Coins;
                    }
                    Destroy(orb.View.gameObject); orbs.RemoveAt(i);
                }
            }
            for (int i=effects.Count-1;i>=0;i--)
            {
                Effect effect=effects[i]; effect.Life-=dt;
                if (effect.Life<=0) { Destroy(effect.View.gameObject); effects.RemoveAt(i); continue; }
                float progress=1-effect.Life/effect.Duration; effect.View.localScale=Vector3.one*(effect.Radius*2*progress);
                effect.Art.SetOpacity(1-progress);
            }
        }
        void ClearProjectiles() { foreach (var shot in shots) if (shot.View!=null) Destroy(shot.View.gameObject); shots.Clear(); }
    }
}
