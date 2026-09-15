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
        enum ScreenMode { Hub, Run, Rewards, Editor, Pause, Result }
        ScreenMode mode = ScreenMode.Hub, editorReturn;
        GameConfig config;
        Profile profile;
        Dungeon dungeon;
        int roomIndex, level, xp, pendingLevels, runCoins, budgetBonus, energyBonus, healthBonus;
        int seed;
        string seedText = "", runId, source, draft, editorMessage = "", toast = "";
        float toastTime, damageBonus, speedBonus, hp, invincible, elapsed, lastCast = -10, transition;
        bool rewardIsLevel, won, banked, saveDirty, speedCastUnlocked;
        string resultTitle;
        HashSet<string> unlocked = new HashSet<string>();
        MageLoadout loadout;
        SpellProgram applied;
        SpellMachine machine;
        Transform roomRoot, player;
        ActorArt playerArt;
        Vector2 playerPosition;
        Camera gameCamera;
        System.Random random;
        readonly List<Rect> obstacles = new List<Rect>();
        readonly List<Reward> rewards = new List<Reward>();
        int energy;
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
        Room CurrentRoom => dungeon.Rooms[roomIndex];
        bool Safe => dungeon == null || CurrentRoom.Cleared;
        string SaveDirectory => Environment.GetEnvironmentVariable("ARCANE_PROFILE_DIR") ?? Application.persistentDataPath;
        string SavePath => Path.Combine(SaveDirectory, "profile.json");

        sealed class Reward { public string Id, Title, Description, Tag; }

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
                profile.Version = Profile.CurrentVersion;
            }
        }
        void LoadEquipment()
        {
            loadout = new MageLoadout(profile.EquippedStaff,profile.EquippedGrimoire);
            SyncEquipmentToProfile();
        }
        void SyncEquipmentToProfile() { profile.EquippedStaff=loadout.Staff; profile.EquippedGrimoire=loadout.Grimoire; }
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
            if (!compiled.Success) { source=SpellCompiler.Starter(MageClassName,PrimarySpellId); compiled=SpellCompiler.Compile(source,Options()); }
            applied=compiled.Program;
            if (dungeon != null) machine=CreateMachine();
        }
        void PrepareHub()
        {
            dungeon = null; draft = null; budgetBonus = energyBonus = healthBonus = 0; damageBonus = speedBonus = 0;
            level = 1; xp = pendingLevels = runCoins = 0; elapsed = 0; lastCast = -10; rewardIsLevel = false; rewards.Clear();
            machine?.Reset(); machine = null;
            mode = ScreenMode.Hub; SetUnlocks(); energy = 0; hp = MaxHealth;
            source = NormalizeMageSource(profile.MageCode);
            var compiled = SpellCompiler.Compile(source, Options());
            if (!compiled.Success) { source = SpellCompiler.Starter(MageClassName,PrimarySpellId); compiled = SpellCompiler.Compile(source, Options()); }
            applied = compiled.Program;
            BuildRoomArt(0, true); SpawnPlayer(Vector2.zero);
        }
        public void StartRun(int requestedSeed, string className)
        {
            string staffId=className=="MagoDeGelo"?"staff-ice":"staff-fire";
            EquipStaffDuringRun(new StaffInstance { DefinitionId=staffId,Level=1 });
            StartRun(requestedSeed);
        }
        void StartRun(int requestedSeed)
        {
            budgetBonus = energyBonus = healthBonus = 0; damageBonus = speedBonus = 0;
            SetUnlocks(); seed = requestedSeed; random = new System.Random(seed); dungeon = Dungeon.Generate(seed);
            runId = Guid.NewGuid().ToString("N"); level = 1; xp = pendingLevels = runCoins = 0; elapsed = 0; lastCast = -10; banked = false; draft = null;
            source = NormalizeMageSource(profile.MageCode);
            var result = SpellCompiler.Compile(source, Options());
            if (!result.Success) { source = SpellCompiler.Starter(MageClassName,PrimarySpellId); result = SpellCompiler.Compile(source, Options()); }
            applied = result.Program; hp = MaxHealth; mode = ScreenMode.Run; EnterRoom(0, Vector2.zero);
        }
        void StartFromHub()
        {
            if (!string.IsNullOrWhiteSpace(seedText) && !int.TryParse(seedText, out seed)) { Notify("A semente deve ser um número inteiro."); return; }
            if (string.IsNullOrWhiteSpace(seedText)) seed = Environment.TickCount;
            StartRun(seed);
        }
        void SpawnPlayer(Vector2 at)
        {
            if (player != null) Destroy(player.gameObject);
            playerPosition=at; playerArt=WorldArt.Actor(transform,"mage",at,Accent,1.25f); player=playerArt.transform;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame)
            {
                Directory.CreateDirectory(SaveDirectory);
                ScreenCapture.CaptureScreenshot(Path.Combine(SaveDirectory,"capture.png"));
            }
            if (toastTime > 0) toastTime -= Time.unscaledDeltaTime;
            if (mode != ScreenMode.Run)
            {
                if (playerArt != null) WorldArt.Sort(playerArt,Time.unscaledTime*3,false);
                if (mode == ScreenMode.Hub && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) StartFromHub();
                if (mode == ScreenMode.Pause && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) mode = ScreenMode.Run;
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { mode = ScreenMode.Pause; return; }
                if (keyboard.tabKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
                { if (Safe) OpenEditor(ScreenMode.Run); else Notify("Limpe a sala ou suba de nível para editar."); if (mode != ScreenMode.Run) return; }
                if (keyboard.qKey.wasPressedThisFrame && Safe)
                {
                    string next=loadout.Staff.DefinitionId=="staff-fire"?"staff-ice":"staff-fire";
                    EquipStaffDuringRun(new StaffInstance { DefinitionId=next,Level=1 });
                }
                if (keyboard.rKey.wasPressedThisFrame && Safe) CycleRicochetRune();
            }
            float dt = Mathf.Min(Time.deltaTime,.05f); elapsed += dt; invincible -= dt; transition -= dt;
            Vector2 direction = Vector2.zero;
            if (keyboard != null)
            {
                direction.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                direction.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            playerPosition = Move(playerPosition, direction.normalized * (config.PlayerSpeed * (1+speedBonus) * dt), .27f);
            player.position = playerPosition; WorldArt.Sort(playerArt, elapsed*12, direction.sqrMagnitude > 0);
            playerArt.Face(direction);
            Color playerColor = playerArt.Directional ? Color.white : Accent;
            playerArt.Body.color = invincible > 0 && Mathf.Sin(elapsed*45) > 0 ? new Color(playerColor.r,playerColor.g,playerColor.b,.4f) : playerColor;
            if (!CurrentRoom.Cleared) machine.Tick(dt);
            UpdateCombat(dt);
            if (hp <= 0) { FinishRun(false); return; }
            if (!CurrentRoom.Cleared && enemies.Count == 0)
            {
                CurrentRoom.Cleared = true; ClearProjectiles();
                RefreshDoors(); Notify("Sala limpa · TAB para abrir o grimório");
                if (CurrentRoom.Kind == RoomKind.Boss) { FinishRun(true); return; }
            }
            if (pendingLevels > 0) { ShowRewards(true); return; }
            if (CurrentRoom.Cleared && transition <= 0) CheckDoors();
        }

        void GainXP(int amount)
        {
            xp += amount;
            while (xp >= NextXP) { xp -= NextXP; level++; pendingLevels++; }
        }
        void ShowRewards(bool forLevel)
        {
            rewardIsLevel = forLevel; mode = ScreenMode.Rewards; rewards.Clear();
            var pool = new List<Reward>
            {
                new Reward { Id="budget", Title="Memória expandida", Description="+2 pontos para equipar código mais complexo.", Tag="COMPLEXIDADE" },
                new Reward { Id="damage", Title="Potência elemental", Description="+15% de dano em todas as suas magias.", Tag="PODER" },
                new Reward { Id="energy", Title="Reservatório arcano", Description="+1 de energia máxima para feitiços carregados.", Tag="ENERGIA" },
                new Reward { Id="health", Title="Vitalidade", Description="+15 de vida máxima e recupera 25 de vida.", Tag="VIGOR" },
                new Reward { Id="speed", Title="Passo etéreo", Description="+8% de velocidade de movimento.", Tag="MOBILIDADE" }
            };
            if (loadout.Elements.Contains(ArcaneElement.Fire) && !unlocked.Contains("flameWave")) pool.Add(new Reward { Id="flameWave", Title="Onda de chamas", Description="Libera this.flameWave() nesta tentativa. Insira a chamada no grimório.", Tag="NOVA MAGIA" });
            if (loadout.Elements.Contains(ArcaneElement.Ice) && !unlocked.Contains("frostNova")) pool.Add(new Reward { Id="frostNova", Title="Nova congelante", Description="Libera this.frostNova() nesta tentativa. Insira a chamada no grimório.", Tag="NOVA MAGIA" });
            while (rewards.Count < 3) { int index = random.Next(pool.Count); rewards.Add(pool[index]); pool.RemoveAt(index); }
        }
        void ChooseReward(Reward reward)
        {
            switch (reward.Id)
            {
                case "budget": budgetBonus += 2; break; case "damage": damageBonus += .15f; break;
                case "energy": energyBonus++; break; case "health": healthBonus += 15; hp = Mathf.Min(MaxHealth,hp+25); break;
                case "speed": speedBonus += .08f; break; default: unlocked.Add(reward.Id); break;
            }
            if (rewardIsLevel) pendingLevels--;
            Notify(reward.Title + " adquirido"); OpenEditor(ScreenMode.Run);
        }
        void OpenEditor(ScreenMode returnTo)
        {
            editorReturn = returnTo; mode = ScreenMode.Editor;
            string saved = profile.MageDraft;
            draft = string.IsNullOrEmpty(saved) ? source : saved;
            editorMessage = "Código ativo preservado até você aplicar as alterações."; ValidateDraft();
        }
        void SaveDraft()
        {
            if (draft == null) return;
            profile.MageDraft = draft;
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
            if (dungeon == null) profile.MageCode = source;
            SaveDraft(); SaveProfile(); editorMessage = "Programa aplicado. A próxima execução começará em constructor()."; ValidateDraft();
        }
        bool EquipStaffDuringRun(StaffInstance staff)
        {
            if (mode == ScreenMode.Run && !Safe || !loadout.EquipStaff(staff)) return false;
            SetUnlocks(); RestoreProgramForLoadout(); SyncEquipmentToProfile();
            string label=MageEquipmentCatalog.TryGetStaff(loadout.Staff.DefinitionId,out StaffDefinition definition) ? definition.Label : loadout.Staff.DefinitionId;
            Notify("Staff equipada: "+label);
            return true;
        }
        bool EquipGrimoire(GrimoireInstance grimoire)
        {
            if (mode == ScreenMode.Run && !Safe || !loadout.EquipGrimoire(grimoire)) return false;
            SetUnlocks(); RestoreProgramForLoadout(); SyncEquipmentToProfile();
            Notify(grimoire==null?"Grimório removido.":"Grimório equipado.");
            return true;
        }
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
            won = victory; resultTitle = victory ? "O código venceu a maldição." : "Até os magos precisam depurar.";
            if (victory) runCoins += 30;
            profile.Bank(runId,runCoins,victory); banked = true; SaveProfile(); mode = ScreenMode.Result;
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
