using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneCode.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace ArcaneCode
{
    public sealed partial class ArcaneGame : MonoBehaviour, ISpellWorld
    {
        const string MageClassName = "MagoArcanista";
        const string CharacterClassId = "mage";
        enum ScreenMode { Hub, Run, Rewards, Shop, Inventory, Editor, Pause, SaveProgram, Result }
        ScreenMode mode = ScreenMode.Hub, editorReturn;
        GameConfig config;
        Profile profile;
        Dungeon dungeon;
        int roomIndex, level, xp, pendingLevels, runCoins, budgetBonus, energyBonus, healthBonus;
        int seed;
        string seedText = "", runId, source, draft, editorMessage = "", toast = "", programSaveName = "", runStartingCode = "", programLoadoutError = "";
        float toastTime, damageBonus, speedBonus, hp, invincible, elapsed, lastCast = -10, transition;
        bool rewardIsLevel, won, banked, saveDirty, speedCastUnlocked;
        bool tutorialRun;
        string tutorialMessage = "";
        float tutorialTime;
        string resultTitle;
        HashSet<string> unlocked = new HashSet<string>();
        MageLoadout loadout;
        RunInventory inventory;
        SpellProgram applied;
        SpellMachine machine;
        Transform roomRoot, player;
        ActorArt playerArt;
        Vector2 playerPosition;
        Camera gameCamera;
        System.Random random;
        readonly List<Rect> obstacles = new List<Rect>();
        readonly List<Reward> rewards = new List<Reward>();
        readonly List<ShopItem> shopItems = new List<ShopItem>();
        readonly Queue<string> tutorialQueue = new Queue<string>();
        readonly HashSet<string> tutorialTipsShown = new HashSet<string>();
        int energy;
        const float ControllerDeadzone = .2f;
        int controllerFocus;
        ScreenMode controllerFocusMode = (ScreenMode)(-1);
        bool controllerConfirmPending;
        Vector2 controllerNavigation;
        float nextControllerNavigation;
        public int Energy => energy;
        public float Health => hp;
        public int EnemyCount => enemies.Count;
        int MaxEnergy => config.InitialEnergy + profile.EnergyRank + energyBonus;
        int Budget => config.InitialBudget + profile.BudgetRank * 2 + budgetBonus;
        float MaxHealth => config.PlayerHealth + profile.HealthRank * 15 + healthBonus;
        int NextXP => 25 + (level - 1) * 15;
        ArcaneElement PrimaryElement => MageEquipmentCatalog.TryGetStaff(loadout?.Staff?.DefinitionId, out StaffDefinition staff) ? staff.Element : ArcaneElement.Fire;
        string PrimarySpellId => MageEquipmentCatalog.TryGetStaff(loadout?.Staff?.DefinitionId, out StaffDefinition staff) ? staff.BaseSpellId : "fireball";
        Color Accent => PrimaryElement == ArcaneElement.Ice ? new Color(.35f,.82f,1) : PrimaryElement == ArcaneElement.Lightning ? new Color(.78f,.58f,1) : new Color(1,.57f,.29f);
        bool HasCurrentRoom => dungeon != null && dungeon.Rooms != null && roomIndex >= 0 && roomIndex < dungeon.Rooms.Count;
        Room CurrentRoom => HasCurrentRoom ? dungeon.Rooms[roomIndex] : null;
        bool Safe => !HasCurrentRoom || CurrentRoom.Cleared;
        string SaveDirectory => Environment.GetEnvironmentVariable("ARCANE_PROFILE_DIR") ?? Application.persistentDataPath;
        string SavePath => Path.Combine(SaveDirectory, "profile.json");
        CharacterProgramState CharacterCode => profile.ProgramFor(CharacterClassId);

        sealed class Reward { public string Id, Title, Description, Tag; }
        sealed class ShopItem { public Reward Reward; public int Price; public bool Sold; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        { if (FindAnyObjectByType<ArcaneGame>() == null) new GameObject("Arcane Code").AddComponent<ArcaneGame>(); }

        void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LoadDevelopmentEnvironment();
#endif
            Application.targetFrameRate = 60;
            config = Resources.Load<GameConfig>("GameConfig") ?? ScriptableObject.CreateInstance<GameConfig>();
            LoadProfile();
            gameCamera = Camera.main;
            if (gameCamera == null) { var obj = new GameObject("Main Camera"); obj.tag = "MainCamera"; gameCamera = obj.AddComponent<Camera>(); }
            gameCamera.orthographic = true; gameCamera.orthographicSize = 6.5f; gameCamera.transform.position = new Vector3(0,0,-10);
            gameCamera.backgroundColor = new Color(.035f,.045f,.075f); gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.transparencySortMode = TransparencySortMode.CustomAxis; gameCamera.transparencySortAxis = Vector3.up;
            if (FindObjectsByType<Light2D>().Length == 0)
            { var light = new GameObject("Luz ambiente").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = .7f; }
            PrepareHub();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void LoadDevelopmentEnvironment()
        {
            DirectoryInfo project=Directory.GetParent(Application.dataPath);
            if (project!=null) DotEnv.LoadFile(Path.Combine(project.FullName,".env"));
        }
#endif

        void LoadProfile()
        {
            profile = new Profile();
            try
            {
                if (File.Exists(SavePath)) profile = JsonUtility.FromJson<Profile>(File.ReadAllText(SavePath)) ?? new Profile();
                MigrateProfile();
                if (profile.Version != Profile.CurrentVersion || profile.Coins < 0) throw new IOException("Formato de save inválido.");
                if (!string.IsNullOrEmpty(profile.MageStartingGrimoireId) && !MageEquipmentCatalog.TryGetGrimoire(profile.MageStartingGrimoireId,out _)) profile.MageStartingGrimoireId="grimoire-fire";
                profile.HealthRank = Mathf.Clamp(profile.HealthRank,0,5); profile.EnergyRank = Mathf.Clamp(profile.EnergyRank,0,5); profile.BudgetRank = Mathf.Clamp(profile.BudgetRank,0,5);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save não carregado: " + e.Message);
                try { if (File.Exists(SavePath)) File.Copy(SavePath, SavePath + ".invalid-" + DateTime.UtcNow.Ticks, false); } catch (IOException) { }
                profile = new Profile(); toast = "Save anterior preservado como backup; novo perfil iniciado."; toastTime = 10;
            }
            LoadEquipment();
        }
        void MigrateProfile()
        {
            if (profile.Version == 1)
            {
                profile.MageCode = string.IsNullOrEmpty(profile.FireCode) ? profile.IceCode : profile.FireCode;
                profile.MageDraft = string.IsNullOrEmpty(profile.FireDraft) ? profile.IceDraft : profile.FireDraft;
                profile.EquippedStaff = new StaffInstance { DefinitionId="staff-fire",Level=1 };
            }
            if (profile.Version <= 2)
            {
                if (profile.Library == null) profile.Library=new List<SavedGrimoire>();
                if (profile.EquippedGrimoire != null && MageLoadout.IsValid(profile.EquippedGrimoire))
                    profile.Library.Add(new SavedGrimoire { Name="Grimório legado", DefinitionId=profile.EquippedGrimoire.DefinitionId, Code=NormalizeMageSource(profile.MageCode) });
                profile.EquippedStaff = new StaffInstance { DefinitionId="staff-fire",Level=1 };
                profile.EquippedGrimoire = null;
            }
            if (profile.Version <= 3)
            {
                profile.TutorialsEnabled = true;
            }
            if (profile.Version <= 4)
            {
                CharacterProgramState program=CharacterCode;
                if (string.IsNullOrEmpty(program.Code)) program.Code=NormalizeMageSource(profile.MageCode) ?? "";
                if (string.IsNullOrEmpty(program.Draft)) program.Draft=NormalizeMageSource(profile.MageDraft) ?? "";
                if (profile.ProgramLibrary == null) profile.ProgramLibrary=new List<SavedCharacterProgram>();
                if (profile.Library != null)
                    foreach (SavedGrimoire legacy in profile.Library)
                    {
                        if (legacy == null || string.IsNullOrWhiteSpace(legacy.Code)) continue;
                        string name=string.IsNullOrWhiteSpace(legacy.Name)?"Programa legado":legacy.Name;
                        string uniqueName=name; int suffix=2;
                        while (profile.ProgramLibrary.Any(entry=>entry != null && entry.ClassId==CharacterClassId && string.Equals(entry.Name,uniqueName,StringComparison.OrdinalIgnoreCase)))
                            uniqueName=name+" (legado "+suffix+++")";
                        profile.ProgramLibrary.Add(new SavedCharacterProgram { Name=uniqueName,ClassId=CharacterClassId,Code=NormalizeMageSource(legacy.Code) });
                    }
            }
            if (profile.Version <= 5)
            {
                profile.MageStartingGrimoireId="grimoire-fire";
                profile.Version=Profile.CurrentVersion;
            }
        }
        void LoadEquipment()
        {
            loadout = new MageLoadout(profile.EquippedStaff,profile.EquippedGrimoire);
            inventory = new RunInventory();
            SyncEquipmentToProfile();
        }
        // Run gear is intentionally not written back to the profile.
        void SyncEquipmentToProfile() { profile.EquippedStaff=new StaffInstance { DefinitionId="staff-fire",Level=1 }; profile.EquippedGrimoire=null; }
        void SaveProfile()
        {
            try
            {
                SyncEquipmentToProfile();
                Directory.CreateDirectory(SaveDirectory);
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(profile, true));
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, SavePath + ".backup"); else File.Move(temp, SavePath);
                saveDirty = false;
            }
            catch (Exception e) { saveDirty = true; Notify("Não foi possível salvar. Seu progresso permanece nesta sessão."); Debug.LogWarning(e.Message); }
        }
        void OnApplicationQuit() { SaveDraft(); SaveProfile(); }
        void Notify(string message) { toast = message; toastTime = 5; }
        void Tutorial(string id,string message)
        {
            if (!tutorialRun || !profile.TutorialsEnabled || !tutorialTipsShown.Add(id)) return;
            tutorialQueue.Enqueue(message);
            if (tutorialTime <= 0) ShowNextTutorial();
        }
        void ShowNextTutorial()
        {
            tutorialMessage = tutorialQueue.Count > 0 ? tutorialQueue.Dequeue() : "";
            tutorialTime = tutorialMessage.Length > 0 ? 7 : 0;
        }

        CompileOptions Options() => new CompileOptions { ClassName = MageClassName, Budget = Budget, Spells = new HashSet<string>(unlocked), SpeedCastUnlocked = speedCastUnlocked };
        SpellMachine CreateMachine() => new SpellMachine(applied,this,config.ChargeSeconds,config.SpellInterval);
        void SetUnlocks()
        {
            unlocked.Clear();
            foreach (string spell in loadout.BaseSpellIds) unlocked.Add(spell);
            foreach (ArcaneElement element in loadout.Elements)
            {
                if (element == ArcaneElement.Fire && profile.FireUnlocked) unlocked.Add("flameWave");
                if (element == ArcaneElement.Ice && profile.IceUnlocked) unlocked.Add("frostNova");
            }
            speedCastUnlocked = profile.SpeedCastUnlocked;
        }
        string NormalizeMageSource(string code)
        {
            if (string.IsNullOrEmpty(code)) return code;
            return code.Replace("class MagoDeFogo extends Mago","class "+MageClassName+" extends Mago").Replace("class MagoDeGelo extends Mago","class "+MageClassName+" extends Mago");
        }
        void RestoreProgramForLoadout()
        {
            source = NormalizeMageSource(source);
            var compiled = SpellCompiler.Compile(source,Options());
            programLoadoutError=compiled.Success?"":compiled.Error.ToString();
            // A missing spell changes execution, never the character's saved code or draft.
            if (!compiled.Success) compiled=SpellCompiler.Compile(SpellCompiler.Starter(MageClassName,PrimarySpellId),Options());
            applied=compiled.Program;
            if (dungeon != null) machine=CreateMachine();
        }
        void LoadCharacterProgram()
        {
            if (string.IsNullOrWhiteSpace(CharacterCode.Code)) CharacterCode.Code=SpellCompiler.Starter(MageClassName,PrimarySpellId);
            source=NormalizeMageSource(CharacterCode.Code);
            RestoreProgramForLoadout();
        }
        void RestoreBasicAttack()
        {
            source=SpellCompiler.Starter(MageClassName,PrimarySpellId);
            CharacterCode.Code=source; RestoreProgramForLoadout(); SaveProfile();
            mode=ScreenMode.Run; Notify("Ataque básico restaurado; rascunho do personagem preservado.");
        }
        void PrepareHub()
        {
            dungeon = null; draft = null; budgetBonus = energyBonus = healthBonus = 0; damageBonus = speedBonus = 0;
            level = 1; xp = pendingLevels = runCoins = 0; elapsed = 0; lastCast = -10; rewardIsLevel = false; rewards.Clear(); shopItems.Clear();
            inventory.BeginRun(CharacterClassId,profile.MageStartingGrimoireId); SyncLoadoutFromInventory();
            machine?.Reset(); machine = null;
            mode = ScreenMode.Hub; SetUnlocks(); energy = 0; hp = MaxHealth;
            LoadCharacterProgram();
            BuildRoomArt(0, true); SpawnPlayer(Vector2.zero);
        }
        public void StartRun(int requestedSeed, string className)
        {
            StartRun(requestedSeed);
            if (className=="MagoDeGelo")
            {
                inventory.EquippedStaff.DefinitionId="staff-ice";
                SyncLoadoutFromInventory(); SetUnlocks(); RestoreProgramForLoadout();
            }
        }
        void StartRun(int requestedSeed)
        {
            budgetBonus = energyBonus = healthBonus = 0; damageBonus = speedBonus = 0;
            tutorialRun = profile.TutorialsEnabled && profile.Runs == 0;
            tutorialMessage = ""; tutorialTime = 0; tutorialQueue.Clear(); tutorialTipsShown.Clear();
            inventory.BeginRun(CharacterClassId,profile.MageStartingGrimoireId); SyncLoadoutFromInventory(); SetUnlocks(); seed = requestedSeed; random = new System.Random(seed); dungeon = Dungeon.Generate(seed);
            runId = Guid.NewGuid().ToString("N"); level = 1; xp = pendingLevels = runCoins = 0; elapsed = 0; lastCast = -10; banked = false; draft = null; rewards.Clear(); shopItems.Clear();
            LoadCharacterProgram(); runStartingCode=CharacterCode.Code; programSaveName="";
            hp = MaxHealth; mode = ScreenMode.Run; EnterRoom(0, Vector2.zero);
        }
        void SyncLoadoutFromInventory()
        {
            RunItemInstance staff=inventory.EquippedStaff;
            RunItemInstance grimoire=inventory.EquippedGrimoire;
            var staffInstance = new StaffInstance { DefinitionId=staff?.DefinitionId ?? "staff-fire", Level=staff?.Level ?? 1 };
            var grimoireInstance = grimoire == null ? null : new GrimoireInstance { DefinitionId=grimoire.DefinitionId,Level=grimoire.Level };
            loadout = new MageLoadout(staffInstance,grimoireInstance);
        }
        void StartFromHub()
        {
            if (!string.IsNullOrWhiteSpace(seedText) && !int.TryParse(seedText, out seed)) { Notify("A semente deve ser um número inteiro."); return; }
            if (string.IsNullOrWhiteSpace(seedText)) seed = Environment.TickCount;
            StartRun(seed);
        }
        bool SelectStartingGrimoire(string id)
        {
            if (mode != ScreenMode.Hub || CharacterClassId != "mage") return false;
            if (!string.IsNullOrEmpty(id) && !MageEquipmentCatalog.TryGetGrimoire(id,out _)) return false;
            profile.MageStartingGrimoireId=id ?? "";
            inventory.BeginRun(CharacterClassId,profile.MageStartingGrimoireId);
            SyncLoadoutFromInventory(); SetUnlocks(); RestoreProgramForLoadout(); SaveProfile();
            Notify(string.IsNullOrEmpty(id)?"Tentativa preparada sem grimório.":"Grimório inicial escolhido · nível 1. Código do personagem preservado.");
            return true;
        }
        void SpawnPlayer(Vector2 at)
        {
            if (player != null) Destroy(player.gameObject);
            playerPosition=at; playerArt=WorldArt.Actor(transform,"mage",at,Accent,1.25f); player=playerArt.transform;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool tutorialCanDisplay = mode == ScreenMode.Run || mode == ScreenMode.Inventory || mode == ScreenMode.Rewards || mode == ScreenMode.Shop;
            if (tutorialTime > 0 && tutorialCanDisplay)
            {
                tutorialTime -= Time.unscaledDeltaTime;
                if (tutorialTime <= 0) ShowNextTutorial();
            }
            if (keyboard != null && keyboard.f12Key.wasPressedThisFrame)
            {
                Directory.CreateDirectory(SaveDirectory);
                ScreenCapture.CaptureScreenshot(Path.Combine(SaveDirectory,"capture.png"));
            }
            if (toastTime > 0) toastTime -= Time.unscaledDeltaTime;
            if (mode != ScreenMode.Run && mode != ScreenMode.Inventory)
            {
                UpdateControllerMenu(gamepad);
                if (playerArt != null) WorldArt.Sort(playerArt,Time.unscaledTime*3,false);
                if (mode == ScreenMode.Hub && keyboard != null && keyboard.enterKey.wasPressedThisFrame) StartFromHub();
                if (mode == ScreenMode.Pause && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) mode = ScreenMode.Run;
                if (mode == ScreenMode.Shop && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) mode = ScreenMode.Run;
                return;
            }
            // A recompilação de scripts durante o Play Mode pode manter a tela da run
            // enquanto os dados transitórios da dungeon foram perdidos. Volta à base
            // antes de consultar CurrentRoom, evitando uma cascata de exceções a cada frame.
            if (!HasCurrentRoom)
            {
                PrepareHub();
                Notify("A tentativa foi reiniciada após a atualização dos scripts.");
                return;
            }
            if (mode == ScreenMode.Inventory)
            {
                if (keyboard != null && (keyboard.iKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame) || gamepad != null && gamepad.selectButton.wasPressedThisFrame)
                { mode=ScreenMode.Run; return; }
            }
            if (keyboard != null || gamepad != null)
            {
                bool pause = keyboard != null && keyboard.escapeKey.wasPressedThisFrame || gamepad != null && gamepad.startButton.wasPressedThisFrame;
                if (pause) { mode = ScreenMode.Pause; return; }

                bool openInventory = keyboard != null && keyboard.iKey.wasPressedThisFrame || gamepad != null && gamepad.selectButton.wasPressedThisFrame;
                if (openInventory) { Tutorial("inventory","I abre a mochila. Clique em um item para ver o efeito e o nível; segure Shift sobre ele para comparar os níveis."); mode=ScreenMode.Inventory; return; }
                bool interact = keyboard != null && keyboard.eKey.wasPressedThisFrame || gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
                if (interact && (TryInteract() || TryBuyShopOffer())) return;
                bool openEditor = keyboard != null && keyboard.tabKey.wasPressedThisFrame || gamepad != null && gamepad.buttonNorth.wasPressedThisFrame;
                if (openEditor)
                {
                    if (Safe) OpenEditor(ScreenMode.Run); else Notify("Limpe a sala ou suba de nível para editar.");
                    if (mode != ScreenMode.Run) return;
                }

            }
            float dt = Mathf.Min(Time.deltaTime,.05f); elapsed += dt; invincible -= dt; transition -= dt;
            Vector2 direction = Vector2.zero;
            if (keyboard != null)
            {
                direction.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                direction.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            Vector2 controllerDirection = ReadControllerDirection(gamepad);
            if (controllerDirection.sqrMagnitude > direction.sqrMagnitude) direction = controllerDirection;
            direction = Vector2.ClampMagnitude(direction,1);
            playerPosition = Move(playerPosition, direction * (config.PlayerSpeed * (1+speedBonus) * dt), .27f);
            player.position = playerPosition;
            // Face primeiro: Sort aplica a animação de caminhada depois da pose base.
            playerArt.Face(direction);
            WorldArt.Sort(playerArt, elapsed*12, direction.sqrMagnitude > 0);
            Color playerColor = playerArt.Directional ? Color.white : Accent;
            playerArt.Body.color = invincible > 0 && Mathf.Sin(elapsed*45) > 0 ? new Color(playerColor.r,playerColor.g,playerColor.b,.4f) : playerColor;
            if (!CurrentRoom.Cleared) machine.Tick(dt);
            UpdateCombat(dt);
            if (hp <= 0) { FinishRun(false); return; }
            if (!CurrentRoom.Cleared && enemies.Count == 0)
            {
                Tutorial("room-clear","Ao limpar uma sala, o XP e as moedas restantes são coletados automaticamente. Siga por uma porta para explorar a próxima sala.");
                CurrentRoom.Cleared = true; ClearProjectiles(); AbsorbRoomResources();
                RefreshDoors(); Notify("Sala limpa · TAB para editar o personagem");
                if (CurrentRoom.Kind == RoomKind.Boss) Notify("Chefe derrotado · 10 moedas caíram. A run continua.");
            }
            if (pendingLevels > 0) { ShowRewards(true); return; }
            if (CurrentRoom.Cleared && transition <= 0) CheckDoors();
        }

        bool ControllerHintsVisible => Gamepad.current != null;

        Vector2 ReadControllerDirection(Gamepad gamepad)
        {
            if (gamepad == null) return Vector2.zero;
            Vector2 stick = gamepad.leftStick.ReadValue();
            Vector2 dpad = gamepad.dpad.ReadValue();
            if (stick.sqrMagnitude < ControllerDeadzone * ControllerDeadzone) stick = Vector2.zero;
            return stick.sqrMagnitude >= dpad.sqrMagnitude ? stick : dpad;
        }

        void UpdateControllerMenu(Gamepad gamepad)
        {
            if (controllerFocusMode != mode)
            {
                controllerFocusMode = mode;
                controllerFocus = mode == ScreenMode.Hub ? 7 : 0;
                controllerConfirmPending = false;
                controllerNavigation = Vector2.zero;
                nextControllerNavigation = 0;
                controllerFocusTargets.Clear();
            }
            if (gamepad == null) return;

            if (gamepad.startButton.wasPressedThisFrame && mode == ScreenMode.Pause) { mode = ScreenMode.Run; return; }
            if (gamepad.buttonEast.wasPressedThisFrame)
            {
                if (mode == ScreenMode.Editor) { CloseEditor(); return; }
                if (mode == ScreenMode.Shop) { mode = ScreenMode.Run; return; }
                if (mode == ScreenMode.Pause) { mode = ScreenMode.Run; return; }
                if (mode == ScreenMode.Result) { PrepareHub(); return; }
            }
            UpdateControllerNavigation(gamepad);
            if (gamepad.buttonSouth.wasPressedThisFrame) controllerConfirmPending = true;
        }

        void UpdateControllerNavigation(Gamepad gamepad)
        {
            Vector2 input = ReadControllerDirection(gamepad);
            if (input.sqrMagnitude < ControllerDeadzone * ControllerDeadzone)
            {
                controllerNavigation = Vector2.zero;
                nextControllerNavigation = 0;
                return;
            }
            Vector2 direction = Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                ? new Vector2(Mathf.Sign(input.x),0)
                : new Vector2(0,Mathf.Sign(input.y));
            bool changed = Vector2.Dot(direction,controllerNavigation) < .99f;
            if (!changed && Time.unscaledTime < nextControllerNavigation) return;
            MoveControllerFocus(direction);
            controllerNavigation = direction;
            nextControllerNavigation = Time.unscaledTime + (changed ? .32f : .12f);
        }

        void GainXP(int amount)
        {
            xp += amount;
            while (xp >= NextXP) { xp -= NextXP; level++; pendingLevels++; }
        }
        void ShowRewards(bool forLevel)
        {
            if (forLevel) Tutorial("level-up","Ao subir de nível, a ação pausa para você escolher um aprimoramento. Escolha um dos três para continuar a run.");
            rewardIsLevel = forLevel; mode = ScreenMode.Rewards; rewards.Clear();
            var pool = RewardPool();
            while (rewards.Count < 3) { int index = random.Next(pool.Count); rewards.Add(pool[index]); pool.RemoveAt(index); }
        }
        List<Reward> RewardPool()
        {
            var pool = new List<Reward>
            {
                new Reward { Id="budget", Title="Memória expandida", Description="+2 pontos para equipar código mais complexo.", Tag="COMPLEXIDADE" },
                new Reward { Id="damage", Title="Potência elemental", Description="+15% de dano em todas as suas magias.", Tag="PODER" },
                new Reward { Id="energy", Title="Reservatório arcano", Description="+1 de energia máxima para feitiços carregados.", Tag="ENERGIA" },
                new Reward { Id="health", Title="Vitalidade", Description="+15 de vida máxima e recupera 25 de vida.", Tag="VIGOR" },
                new Reward { Id="speed", Title="Passo etéreo", Description="+8% de velocidade de movimento.", Tag="MOBILIDADE" }
            };
            if (loadout.Elements.Contains(ArcaneElement.Fire) && !unlocked.Contains("flameWave")) pool.Add(new Reward { Id="flameWave", Title="Onda de chamas", Description="Libera this.flameWave() nesta tentativa. Insira a chamada no programa do personagem.", Tag="NOVA MAGIA" });
            if (loadout.Elements.Contains(ArcaneElement.Ice) && !unlocked.Contains("frostNova")) pool.Add(new Reward { Id="frostNova", Title="Nova congelante", Description="Libera this.frostNova() nesta tentativa. Insira a chamada no programa do personagem.", Tag="NOVA MAGIA" });
            return pool;
        }
        int ShopPrice(Reward reward)
        {
            return reward.Id == "damage" || reward.Id == "speed" ? 15 : reward.Id == "flameWave" || reward.Id == "frostNova" ? 20 : 12;
        }
        void ShowShop()
        {
            if (!CurrentRoom.Claimed)
            {
                CurrentRoom.Claimed = true; shopItems.Clear();
                var pool = RewardPool();
                while (shopItems.Count < 3)
                {
                    int index = random.Next(pool.Count); Reward reward = pool[index]; pool.RemoveAt(index);
                    shopItems.Add(new ShopItem { Reward = reward, Price = ShopPrice(reward) });
                }
            }
            mode = ScreenMode.Shop;
        }
        void ChooseReward(Reward reward)
        {
            ApplyReward(reward);
            if (rewardIsLevel) pendingLevels--;
            Notify(reward.Title + " adquirido"); mode=ScreenMode.Inventory;
        }
        void ApplyReward(Reward reward)
        {
            switch (reward.Id)
            {
                case "budget": budgetBonus += 2; break; case "damage": damageBonus += .15f; break;
                case "energy": energyBonus++; break; case "health": healthBonus += 15; hp = Mathf.Min(MaxHealth,hp+25); break;
                case "speed": speedBonus += .08f; break; default: unlocked.Add(reward.Id); break;
            }
        }
        void BuyShopItem(ShopItem item)
        {
            if (item.Sold || runCoins < item.Price) return;
            runCoins -= item.Price; item.Sold = true; ApplyReward(item.Reward);
            Notify(item.Reward.Title + " adquirido na loja");
        }
        void OpenEditor(ScreenMode returnTo)
        {
            editorReturn = returnTo; mode = ScreenMode.Editor;
            string saved = CharacterCode.Draft;
            draft = string.IsNullOrEmpty(saved) ? source : saved;
            ResetDraftHistory();
            editorMessage = string.IsNullOrEmpty(programLoadoutError)?"Programa do personagem preservado até você aplicar as alterações.":"Código preservado. Ataque básico temporário até o equipamento liberar as magias usadas."; ValidateDraft();
        }
        void SaveDraft()
        {
            if (draft == null) return;
            CharacterCode.Draft=draft;
        }
        void CloseEditor()
        {
            SaveDraft(); SaveProfile(); mode = editorReturn;
            if (mode == ScreenMode.Run && pendingLevels > 0) ShowRewards(true);
        }
        void ApplyDraft()
        {
            var compiled = SpellCompiler.Compile(draft,Options());
            if (!compiled.Success) { editorMessage = compiled.Error.ToString(); return; }
            source = draft; applied = compiled.Program; machine = CreateMachine();
            CharacterCode.Code=source; programLoadoutError="";
            SaveDraft(); SaveProfile(); editorMessage = "Programa aplicado. A próxima execução começará em constructor()."; ValidateDraft();
        }
        bool EquipStaffDuringRun(StaffInstance staff)
        {
            if (mode == ScreenMode.Run && !Safe || !loadout.EquipStaff(staff)) return false;
            SetUnlocks(); RestoreProgramForLoadout(); SyncEquipmentToProfile();
            string label=MageEquipmentCatalog.TryGetStaff(loadout.Staff.DefinitionId,out StaffDefinition definition) ? definition.Label : loadout.Staff.DefinitionId;
            Notify("Staff equipada: "+label+(string.IsNullOrEmpty(programLoadoutError)?"":" · Código preservado; ataque básico temporário."));
            return true;
        }
        bool EquipGrimoire(GrimoireInstance grimoire)
        {
            if (mode == ScreenMode.Run && !Safe || !loadout.EquipGrimoire(grimoire)) return false;
            SetUnlocks(); RestoreProgramForLoadout(); SyncEquipmentToProfile();
            Notify((grimoire==null?"Grimório removido.":"Grimório equipado.")+(string.IsNullOrEmpty(programLoadoutError)?"":" Código preservado; ataque básico temporário."));
            return true;
        }
        bool EquipInventoryItem(RunItemInstance item)
        {
            if (item == null || !RunItemCatalog.TryGet(item.DefinitionId,out RunItemDefinition definition) || !inventory.Equip(item.InstanceId,CharacterClassId)) return false;
            SyncLoadoutFromInventory();
            SetUnlocks(); RestoreProgramForLoadout();
            Notify(definition.Label+" equipado."+(string.IsNullOrEmpty(programLoadoutError)?"":" Código preservado; ataque básico temporário.")); return true;
        }
        void SaveCharacterProgram()
        {
            if (string.IsNullOrWhiteSpace(programSaveName)) { Notify("Dê um nome ao programa antes de guardar a cópia."); return; }
            if (profile.ProgramLibrary == null) profile.ProgramLibrary=new List<SavedCharacterProgram>();
            profile.ProgramLibrary.RemoveAll(entry=>entry != null && entry.ClassId==CharacterClassId && string.Equals(entry.Name,programSaveName.Trim(),StringComparison.OrdinalIgnoreCase));
            profile.ProgramLibrary.Add(new SavedCharacterProgram { Name=programSaveName.Trim(),ClassId=CharacterClassId,Code=CharacterCode.Code });
            SaveProfile(); mode=ScreenMode.Result; Notify("Programa do personagem salvo na Biblioteca.");
        }
        void SkipSaveProgram() { mode=ScreenMode.Result; }
        void CycleRicochetRune()
        {
            var candidate = new StaffInstance { DefinitionId=loadout.Staff.DefinitionId,Level=loadout.Staff.Level };
            int rank=0;
            foreach (RuneInstance rune in loadout.Staff.Runes)
            {
                if (rune.DefinitionId == "rune-ricochet") rank=rune.Rank;
                else candidate.Runes.Add(new RuneInstance { DefinitionId=rune.DefinitionId,Rank=rune.Rank });
            }
            rank=(rank+1)%4;
            if (rank>0) candidate.Runes.Add(new RuneInstance { DefinitionId="rune-ricochet",Rank=rank });
            if (EquipStaffDuringRun(candidate)) Notify(rank==0?"Runa de ricochete removida.":"Runa de ricochete nível "+rank+" equipada.");
        }
        void FinishRun(bool victory)
        {
            if (banked) return;
            tutorialRun=false; tutorialQueue.Clear(); tutorialMessage=""; tutorialTime=0;
            won = victory; resultTitle = victory ? "O código venceu a maldição." : "Até os magos precisam depurar.";
            SaveDraft(); profile.Bank(runId,runCoins,victory); banked = true; SaveProfile();
            mode = CharacterCode.Code != runStartingCode ? ScreenMode.SaveProgram : ScreenMode.Result;
            ClearProjectiles(); machine?.Reset();
        }
        void Buy(string id)
        {
            int rank = id == "health" ? profile.HealthRank : id == "energy" ? profile.EnergyRank : profile.BudgetRank;
            bool magic = id == "fire" || id == "ice" || id == "speedCast";
            int price = magic ? 75 : 20 + rank*20;
            bool alreadyUnlocked = id == "fire" ? profile.FireUnlocked : id == "ice" ? profile.IceUnlocked : id == "speedCast" && profile.SpeedCastUnlocked;
            if (profile.Coins < price || !magic && rank >= 5 || alreadyUnlocked) return;
            profile.Coins -= price;
            switch (id) { case "health": profile.HealthRank++; break; case "energy": profile.EnergyRank++; break; case "budget": profile.BudgetRank++; break; case "fire": profile.FireUnlocked = true; break; case "ice": profile.IceUnlocked = true; break; case "speedCast": profile.SpeedCastUnlocked = true; break; }
            SaveProfile(); PrepareHub(); Notify("Melhoria permanente adquirida.");
        }
        public void AddEnergy(int units) { energy = Mathf.Min(MaxEnergy,energy+units); }
    }
}
