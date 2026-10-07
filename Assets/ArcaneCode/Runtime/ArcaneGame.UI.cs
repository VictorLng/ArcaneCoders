using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ArcaneCode.Core;
using UnityEngine;

namespace ArcaneCode
{
    public sealed partial class ArcaneGame
    {
        static readonly Color Background = new Color(.035f,.045f,.075f,.97f);
        static readonly Color PanelColor = new Color(.075f,.095f,.145f,.98f);
        static readonly Color Muted = new Color(.56f,.64f,.75f);
        static readonly Color White = new Color(.91f,.94f,1);
        GUIStyle textStyle, smallStyle, titleStyle, codeStyle, codeInputStyle, buttonStyle;
        Vector2 codeScroll, docsScroll;
        CompileResult draftResult;
        string highlighted = "", highlightedSource = "";
        int caret, selectionCaret;
        bool focusCode;
        const int DraftHistoryLimit = 100;
        struct DraftEdit { public string Text; public int Cursor, Selection; }
        readonly List<DraftEdit> draftUndo = new List<DraftEdit>();
        readonly List<DraftEdit> draftRedo = new List<DraftEdit>();
        float uiScale;
        sealed class Completion { public string Label, Insert, Detail; }
        readonly List<Completion> completions = new List<Completion>();
        bool completionVisible;
        int completionIndex;
        bool inventoryStatsOpen;
        string selectedInventoryItemId;
        readonly List<Rect> controllerFocusTargets = new List<Rect>();

        void Styles()
        {
            if (textStyle!=null) return;
            Font uiFont=Resources.Load<Font>("DejaVuSans");
            textStyle=new GUIStyle(GUI.skin.label) { font=uiFont,fontSize=17,wordWrap=true,richText=false }; textStyle.normal.textColor=White;
            smallStyle=new GUIStyle(textStyle) { fontSize=13 };
            titleStyle=new GUIStyle(textStyle) { fontSize=38,fontStyle=FontStyle.Bold };
            Font mono=Resources.Load<Font>("DejaVuSansMono");
            codeStyle=new GUIStyle(textStyle) { font=mono,fontSize=16,wordWrap=false,richText=true,padding=new RectOffset(5,5,4,4) };
            codeInputStyle=new GUIStyle(GUI.skin.textArea) { font=mono,fontSize=16,wordWrap=false,richText=false,padding=new RectOffset(5,5,4,4),border=new RectOffset(0,0,0,0),margin=new RectOffset(0,0,0,0) };
            foreach (GUIStyleState state in new[] {codeInputStyle.normal,codeInputStyle.hover,codeInputStyle.active,codeInputStyle.focused}) { state.background=Texture2D.blackTexture; state.textColor=new Color(1,1,1,0); }
            buttonStyle=new GUIStyle { font=uiFont,fontSize=15,fontStyle=FontStyle.Bold,wordWrap=false,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(12,12,5,5) };
            buttonStyle.normal.textColor=White; buttonStyle.hover.textColor=White; buttonStyle.active.textColor=White; buttonStyle.focused.textColor=White;
            GUI.skin.settings.cursorColor=new Color(.6f,.9f,1);
            GUI.skin.settings.selectionColor=new Color(.2f,.45f,.65f,.55f);
        }
        void Box(Rect rect,Color color) { Color old=GUI.color; GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=old; }
        void Label(Rect rect,string text,int size=17,Color? color=null,bool bold=false)
        { var style=new GUIStyle(textStyle) {fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal}; style.normal.textColor=color??White; rect.height=Mathf.Max(rect.height,style.lineHeight+4); GUI.Label(rect,text,style); }
        bool Button(Rect rect,string label,bool primary=false,bool enabled=true)
        {
            int controllerIndex = -1;
            if (enabled)
            {
                controllerIndex = controllerFocusTargets.Count;
                controllerFocusTargets.Add(rect);
            }
            bool controllerFocused = ControllerHintsVisible && controllerIndex == controllerFocus;
            bool controllerClicked = controllerFocused && controllerConfirmPending;
            if (controllerClicked) controllerConfirmPending = false;
            bool previous=GUI.enabled; GUI.enabled=enabled;
            bool hover=rect.Contains(Event.current.mousePosition)&&enabled;
            Color fill=primary?new Color(Accent.r*.28f,Accent.g*.28f,Accent.b*.28f):new Color(.13f,.18f,.26f);
            if (hover) fill*=1.35f; if (!enabled) fill*=.65f;
            Box(rect,fill); Box(new Rect(rect.x,rect.y,rect.width,2),enabled&&primary?Accent:new Color(.25f,.31f,.4f));
            if (controllerFocused)
            {
                Box(new Rect(rect.x,rect.y,rect.width,2),Accent);
                Box(new Rect(rect.x,rect.y+rect.height-2,rect.width,2),Accent);
                Box(new Rect(rect.x,rect.y,2,rect.height),Accent);
                Box(new Rect(rect.x+rect.width-2,rect.y,2,rect.height),Accent);
            }
            var style=new GUIStyle(buttonStyle); while (style.fontSize>11 && style.CalcSize(new GUIContent(label)).x>rect.width) style.fontSize--;
            bool clicked=GUI.Button(rect,label,style); GUI.enabled=previous; return clicked || controllerClicked;
        }
        void MoveControllerFocus(Vector2 direction)
        {
            if (controllerFocusTargets.Count==0) return;
            // Gamepad up is positive Y, while IMGUI rectangles grow downward.
            direction.y = -direction.y;
            controllerFocus=Mathf.Clamp(controllerFocus,0,controllerFocusTargets.Count-1);
            Vector2 origin=controllerFocusTargets[controllerFocus].center;
            int best=-1; float bestScore=float.NegativeInfinity;
            for (int i=0;i<controllerFocusTargets.Count;i++)
            {
                if (i==controllerFocus) continue;
                Vector2 delta=controllerFocusTargets[i].center-origin;
                if (delta.sqrMagnitude<.01f) continue;
                float alignment=Vector2.Dot(delta.normalized,direction);
                if (alignment<.2f) continue;
                float score=alignment*1000f-delta.magnitude;
                if (score>bestScore) { bestScore=score; best=i; }
            }
            if (best>=0) controllerFocus=best;
        }
        void ControllerHint(Rect rect,string text)
        {
            if (!ControllerHintsVisible) return;
            Box(rect,new Color(.06f,.1f,.16f,.94f));
            Label(new Rect(rect.x+12,rect.y+5,rect.width-24,rect.height-8),text,12,Muted);
        }
        void Bar(Rect rect,float value,Color color)
        { Box(rect,new Color(.12f,.16f,.23f)); Box(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(value),rect.height),color); }
        void OnGUI()
        {
            Styles();
            controllerFocusTargets.Clear();
            uiScale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*uiScale)/2,(Screen.height-800*uiScale)/2,0),Quaternion.identity,Vector3.one*uiScale);
            switch (mode)
            {
                case ScreenMode.Hub: HubUI(); break;
                case ScreenMode.Run: HUD(); break;
                case ScreenMode.Rewards: HUD(); RewardsUI(); break;
                case ScreenMode.Shop: HUD(); ShopUI(); break;
                case ScreenMode.Inventory: HUD(); InventoryUI(); break;
                case ScreenMode.Editor: EditorUI(); break;
                case ScreenMode.Pause: HUD(); PauseUI(); break;
                case ScreenMode.SaveProgram: SaveProgramUI(); break;
                case ScreenMode.Result: ResultUI(); break;
            }
            if (mode==ScreenMode.Run && transition>0) Box(new Rect(0,0,1280,800),new Color(.02f,.03f,.05f,Mathf.Clamp01(transition/.7f)));
            if (!string.IsNullOrEmpty(tutorialMessage) && mode == ScreenMode.Inventory)
            {
                Box(new Rect(312,686,916,54),PanelColor);
                Box(new Rect(312,686,4,54),Accent);
                Label(new Rect(328,693,884,42),"TUTORIAL  ·  "+tutorialMessage,13,White);
            }
            else if (!string.IsNullOrEmpty(tutorialMessage) && (mode == ScreenMode.Rewards || mode == ScreenMode.Shop))
            {
                Box(new Rect(52,183,1176,48),PanelColor);
                Box(new Rect(52,183,4,48),Accent);
                Label(new Rect(68,190,1148,36),"TUTORIAL OPCIONAL  ·  "+tutorialMessage,13,White);
            }
            else if (!string.IsNullOrEmpty(tutorialMessage) && mode == ScreenMode.Run)
            {
                Box(new Rect(385,205,510,104),PanelColor);
                Box(new Rect(385,205,4,104),Accent);
                Label(new Rect(405,216,470,22),"TUTORIAL OPCIONAL",12,Accent,true);
                Label(new Rect(405,241,470,58),tutorialMessage,15,White);
            }
            if (toastTime>0)
            { Box(new Rect(240,742,800,40),PanelColor); Label(new Rect(255,750,770,28),toast,14,Accent); }
            GUI.matrix=Matrix4x4.identity;
        }
        void Header(string eyebrow,string title,string description)
        {
            Label(new Rect(52,36,1000,24),eyebrow,13,Accent,true);
            Label(new Rect(48,64,1120,60),title,42,White,true);
            Label(new Rect(52,126,1120,48),description,16,Muted);
        }
        void HubUI()
        {
            Box(new Rect(0,0,1280,800),Background);
            Header("O SANTUÁRIO  /  PREPARE A TENTATIVA","ARCANE CODERS","Escolha o grimório inicial do mago. Encontre novos equipamentos dentro da dungeon.");
            Label(new Rect(977,40,250,30),"◈  "+profile.Coins+" fragmentos",18,Accent,true);
            Label(new Rect(977,75,250,24),profile.Runs+" tentativas · "+profile.Wins+" vitórias",13,Muted);
            if (Button(new Rect(977,108,250,36),profile.TutorialsEnabled?"Tutoriais: ligados":"Tutoriais: desligados"))
            {
                profile.TutorialsEnabled=!profile.TutorialsEnabled;
                if (!profile.TutorialsEnabled) { tutorialRun=false; tutorialQueue.Clear(); tutorialMessage=""; tutorialTime=0; }
                SaveProfile(); Notify(profile.TutorialsEnabled?"Tutoriais ativados.":"Tutoriais desativados.");
            }
            Box(new Rect(52,195,722,215),PanelColor);
            Label(new Rect(76,210,650,26),"MAGO ARCANO  /  GRIMÓRIO INICIAL",16,Accent,true);
            Label(new Rect(76,240,650,23),"Staff de Fogo + grimório escolhido, ambos no nível 1. Seu código permanece.",13,Muted);
            GrimoireDefinition[] startingGrimoires=MageEquipmentCatalog.StartingGrimoires.ToArray();
            for (int index=0;index<startingGrimoires.Length;index++)
                StartingGrimoireCard(new Rect(76+index*170,272,157,84),startingGrimoires[index]);
            Label(new Rect(76,373,480,30),"POOL: "+string.Join(" · ",unlocked.OrderBy(spell=>spell).Select(spell=>"this."+spell+"()")),12,Muted);
            bool noGrimoire=string.IsNullOrEmpty(profile.MageStartingGrimoireId);
            if (Button(new Rect(586,369,157,31),noGrimoire?"Sem grimório ✓":"Sem grimório",noGrimoire)) SelectStartingGrimoire(null);
            Box(new Rect(52,434,722,256),PanelColor);
            Label(new Rect(75,453,650,26),"O PERSONAGEM TEM O CÓDIGO. O GRIMÓRIO LIBERA FEITIÇOS.",14,Accent,true);
            Label(new Rect(75,487,650,42),"Seu programa e rascunho continuam ao trocar equipamentos.\nTAB edita o personagem; I abre a mochila durante a run.",16);
            Label(new Rect(75,599,180,20),"Semente opcional",13,Muted);
            seedText=GUI.TextField(new Rect(75,622,188,34),seedText,11);
            if (Button(new Rect(285,615,215,43),"Programar personagem")) OpenEditor(ScreenMode.Hub);
            if (Button(new Rect(520,615,228,43),"Entrar na dungeon →",true)) StartFromHub();
            Box(new Rect(803,195,425,495),PanelColor);
            Label(new Rect(826,215,380,26),"LEGADO PERMANENTE",15,Accent,true);
            UpgradeRow(263,"health","Vitalidade","+15 de vida inicial",profile.HealthRank);
            UpgradeRow(335,"energy","Reservatório","+1 de energia máxima",profile.EnergyRank);
            UpgradeRow(407,"budget","Memória arcana","+2 de complexidade inicial",profile.BudgetRank);
            UpgradeRow(479,"fire","Onda de chamas","Libera flameWave() na base",profile.FireUnlocked?1:0,true);
            UpgradeRow(551,"ice","Nova congelante","Libera frostNova() na base",profile.IceUnlocked?1:0,true);
            UpgradeRow(623,"speedCast","Conjuração célere","Libera speedCast() para fogo e gelo",profile.SpeedCastUnlocked?1:0,true);
            Label(new Rect(52,708,1100,25),ControllerHintsVisible?"CONTROLE  /  ANALÓGICO OU D-PAD: NAVEGAR  ·  BOTÃO INFERIOR: CONFIRMAR":"PROTÓTIPO 0.1     /     FOGO + GELO     /     UMA DUNGEON, INFINITAS REVISÕES",12,Muted);
            if (Button(new Rect(1090,701,138,35),"Sair")) Application.Quit();
        }
        void StartingGrimoireCard(Rect rect,GrimoireDefinition definition)
        {
            bool selected=profile.MageStartingGrimoireId==definition.Id;
            Box(rect,new Color(.045f,.06f,.1f)); Box(new Rect(rect.x,rect.y,rect.width,3),selected?Accent:Muted);
            string title=RunItemCatalog.TryGet(definition.Id,out RunItemDefinition item)?item.Label.Replace("Grimório da ",""):definition.Label;
            Label(new Rect(rect.x+8,rect.y+9,rect.width-16,35),title,13,selected?Accent:White,true);
            if (Button(new Rect(rect.x+8,rect.y+51,rect.width-16,26),selected?"Selecionado ✓":"Escolher",selected)) SelectStartingGrimoire(definition.Id);
        }
        void StaffCard(Rect rect,string id,string label,string description,Color color)
        {
            bool selected=loadout.Staff.DefinitionId==id; Box(rect,PanelColor); Box(new Rect(rect.x,rect.y,rect.width,3),selected?color:new Color(.2f,.24f,.33f));
            Label(new Rect(rect.x+20,rect.y+20,rect.width-40,25),label,14,color,true);
            Label(new Rect(rect.x+20,rect.y+60,rect.width-40,26),selected?"EQUIPADA":"DISPONÍVEL",23,White,true);
            Label(new Rect(rect.x+20,rect.y+101,rect.width-40,58),description,16,Muted);
            if (Button(new Rect(rect.x+20,rect.y+163,rect.width-40,35),selected?"Staff equipada":"Equipar staff",selected))
            { SaveDraft(); EquipStaffDuringRun(new StaffInstance { DefinitionId=id,Level=1 }); draft=null; PrepareHub(); }
        }
        void GrimoireButton(Rect rect,string id,string label)
        {
            bool selected=(loadout.Grimoire?.DefinitionId??string.Empty)==(id??string.Empty);
            if (Button(rect,selected?label+" ✓":label,selected)) EquipGrimoire(id==null?null:new GrimoireInstance { DefinitionId=id,Level=1 });
        }
        void UpgradeRow(float y,string id,string title,string description,int rank,bool magic=false)
        {
            bool max=rank>=(magic?1:5); int price=magic?75:20+rank*20;
            Label(new Rect(826,y,230,23),title+(magic?"":"  "+rank+"/5"),16,White,true);
            Label(new Rect(826,y+25,238,38),description,12,Muted);
            if (Button(new Rect(1080,y+2,125,43),max?"Adquirido":price+" ◈",false,!max&&profile.Coins>=price)) Buy(id);
        }
        void HUD()
        {
            if (!HasCurrentRoom)
            {
                HubUI();
                return;
            }
            Box(new Rect(22,20,382,98),Background);
            string staffLabel=MageEquipmentCatalog.TryGetStaff(loadout.Staff.DefinitionId,out StaffDefinition staff)?staff.Label:loadout.Staff.DefinitionId;
            Label(new Rect(38,29,350,25),"MAGO ARCANO  /  "+staffLabel.ToUpperInvariant()+" NV. "+loadout.Staff.Level+"  /  NV. "+level,16,Accent,true);
            Bar(new Rect(38,65,270,10),hp/MaxHealth,new Color(.89f,.3f,.42f)); Label(new Rect(318,56,80,28),Mathf.CeilToInt(hp)+" / "+MaxHealth,12);
            Bar(new Rect(38,91,350,4),(float)xp/NextXP,new Color(.38f,.92f,.74f));
            Box(new Rect(430,20,385,76),Background);
            int codeCost=applied!=null?applied.Cost:0;
            Label(new Rect(446,30,350,22),"ENERGIA   "+energy+" / "+MaxEnergy+"       CÓDIGO   "+codeCost+" / "+Budget,14,Accent,true);
            for (int i=0;i<MaxEnergy;i++) Box(new Rect(449+i*Mathf.Min(28,340f/MaxEnergy),64,Mathf.Min(21,300f/MaxEnergy),8),i<energy?Accent:new Color(.17f,.22f,.3f));
            if (machine!=null && machine.Charging) Bar(new Rect(446,82,350,3),machine.ChargeProgress,Accent);
            MiniMap();
            Box(new Rect(22,693,720,39),Background);
            string controls=ControllerHintsVisible
                ? CurrentRoom.Cleared?"SALA SEGURA   /   VIEW: mochila · BOTÃO SUPERIOR: programar · MENU: pausar":"ANALÓGICO OU D-PAD: mover   /   Ataques automáticos   /   MENU: pausar"
                : CurrentRoom.Cleared?"SALA SEGURA   /   I: mochila · E: interagir · TAB: programar personagem":"WASD: mover   /   Ataques automáticos   /   I: mochila · E: interagir";
            Label(new Rect(37,702,690,25),controls,13,Muted);
            Label(new Rect(22,128,390,24),"ANDAR "+CurrentRoom.Floor+"/5   ◈ "+runCoins+"     TEMPO "+TimeSpan.FromSeconds(elapsed).ToString(@"mm\:ss")+"     XP "+xp+"/"+NextXP,13,White);
            if (machine!=null && machine.Error!=null)
            { Box(new Rect(260,615,760,62),Background); Label(new Rect(278,624,724,52),"Execução interrompida: "+machine.Error+"\nESC → restaurar ataque básico para continuar.",14,new Color(1,.55f,.5f)); }
            else if (!string.IsNullOrEmpty(programLoadoutError))
            { Box(new Rect(260,615,760,62),Background); Label(new Rect(278,624,724,52),"Ataque básico temporário · seu código foi preservado.\nEquipe as magias necessárias ou use TAB em uma sala limpa para editar.",14,new Color(1,.77f,.43f)); }
            Enemy boss=enemies.FirstOrDefault(e=>e.Boss);
            if (boss!=null)
            {
                Box(new Rect(360,641,560,45),Background); Label(new Rect(374,646,530,22),boss.Definition.Label.ToUpperInvariant(),14,new Color(1,.53f,.57f),true);
                Bar(new Rect(375,675,530,5),boss.HP/boss.MaxHP,new Color(.86f,.25f,.4f));
            }
        }
        void MiniMap()
        {
            if (dungeon == null || dungeon.Rooms == null || dungeon.Rooms.Count == 0) return;
            Box(new Rect(1020,20,238,192),Background); Label(new Rect(1036,31,200,24),"CATACUMBAS / "+seed,11,Muted);
            int minX=dungeon.Rooms.Min(r=>r.X),maxX=dungeon.Rooms.Max(r=>r.X),minY=dungeon.Rooms.Min(r=>r.Y),maxY=dungeon.Rooms.Max(r=>r.Y);
            float size=Mathf.Min(28,Mathf.Min(195f/(maxX-minX+1),134f/(maxY-minY+1)));
            for (int i=0;i<dungeon.Rooms.Count;i++)
            {
                Room room=dungeon.Rooms[i]; bool known=room.Visited || room.Neighbors.Any(n=>dungeon.Rooms[n].Visited);
                if (!known) continue;
                float x=1037+(room.X-minX)*size,y=63+(maxY-room.Y)*size;
                foreach (int n in room.Neighbors)
                {
                    Room neighbor=dungeon.Rooms[n]; if (!room.Visited&&!neighbor.Visited) continue;
                    float nx=1037+(neighbor.X-minX)*size,ny=63+(maxY-neighbor.Y)*size;
                    Box(new Rect(Mathf.Min(x,nx)+size*.35f,Mathf.Min(y,ny)+size*.35f,Mathf.Abs(nx-x)+3,Mathf.Abs(ny-y)+3),new Color(.27f,.31f,.43f));
                }
                Color color=i==roomIndex?Accent:room.Cleared&&room.Visited?new Color(.33f,.52f,.56f):new Color(.23f,.28f,.39f);
                Box(new Rect(x,y,size*.8f,size*.8f),color);
                string symbol=room.Kind==RoomKind.Boss?"B":room.Kind==RoomKind.Shop?"$":room.Kind==RoomKind.Rest?"♡":room.Kind==RoomKind.Treasure?"▣":"";
                Label(new Rect(x+3,y-1,size,size),symbol,11,White,true);
            }
        }
        void RewardsUI()
        {
            Box(new Rect(0,0,1280,800),new Color(.02f,.025f,.05f,.91f));
            Header(rewardIsLevel?"NÍVEL "+level+"  /  EXECUÇÃO PAUSADA":"SALA DE RECOMPENSA","Uma nova possibilidade.","Escolha uma melhoria. Depois, abra espaço para ela no seu código.");
            for (int i=0;i<rewards.Count;i++)
            {
                float x=52+i*397; Reward reward=rewards[i];
                Box(new Rect(x,242,376,346),PanelColor); Box(new Rect(x,242,376,3),Accent);
                Label(new Rect(x+24,273,326,26),"0"+(i+1)+"  /  "+reward.Tag,13,Accent,true);
                Label(new Rect(x+24,326,326,64),reward.Title,27,White,true);
                Label(new Rect(x+24,405,326,90),reward.Description,18,Muted);
                if (Button(new Rect(x+24,516,328,48),"Escolher melhoria",true)) { ChooseReward(reward); break; }
            }
            Label(new Rect(52,633,1100,35),"A sintaxe inteira está disponível desde o início. Você decide como combinar as funções.",16,Muted);
            ControllerHint(new Rect(52,680,720,32),"ANALÓGICO OU D-PAD: navegar  ·  BOTÃO INFERIOR: escolher");
        }
        void ShopUI()
        {
            Box(new Rect(0,0,1280,800),new Color(.02f,.025f,.05f,.91f));
            Header("LOJA ARCANA  /  MOEDAS DA TENTATIVA: "+runCoins,"MERCADOR DAS CATACUMBAS","Compre melhorias temporárias para esta tentativa. Itens comprados não voltam na próxima run.");
            for (int i=0;i<shopItems.Count;i++)
            {
                float x=52+i*397; ShopItem item=shopItems[i]; Reward reward=item.Reward;
                Box(new Rect(x,242,376,346),PanelColor); Box(new Rect(x,242,376,3),item.Sold?Muted:Accent);
                Label(new Rect(x+24,273,326,26),"0"+(i+1)+"  /  "+reward.Tag,13,Accent,true);
                Label(new Rect(x+24,326,326,64),reward.Title,27,White,true);
                Label(new Rect(x+24,405,326,90),reward.Description,18,Muted);
                string label=item.Sold?"Esgotado":item.Price+" ◈";
                if (Button(new Rect(x+24,516,328,48),label,true,!item.Sold&&runCoins>=item.Price)) { BuyShopItem(item); break; }
            }
            if (Button(new Rect(52,623,280,43),"Sair da loja")) mode=ScreenMode.Run;
            Label(new Rect(350,632,760,25),"Moedas só entram no saldo quando você as coleta no chão.",14,Muted);
            ControllerHint(new Rect(52,690,720,32),"ANALÓGICO OU D-PAD: navegar  ·  BOTÃO INFERIOR: comprar  ·  BOTÃO DIREITO: sair");
        }
        Color ItemColor(RunItemDefinition definition)
        {
            if (definition.OwnerClassId == "goblin") return new Color(1,.72f,.24f);
            return definition.Kind == RunItemKind.Ring ? new Color(.7f,.38f,1) : new Color(.86f,.39f,1);
        }
        void InventoryUI()
        {
            Box(new Rect(0,0,1280,800),new Color(.015f,.02f,.04f,.9f));
            Header("MOCHILA  /  AÇÃO CONTINUA EM TEMPO REAL","INVENTÁRIO DA TENTATIVA","Clique em staffs e grimórios para equipar. Passe o cursor para ler; segure Shift para ver os níveis.");
            Box(new Rect(52,205,305,478),PanelColor);
            MagePortrait(new Rect(76,237,140,140));
            if (Button(new Rect(232,237,92,38),"⌕ Status")) inventoryStatsOpen=!inventoryStatsOpen;
            Label(new Rect(76,402,240,24),"NÍVEL "+level+"  ·  VIDA "+Mathf.CeilToInt(hp)+"/"+Mathf.CeilToInt(MaxHealth),14,Muted);
            Label(new Rect(76,434,240,24),"STAFF",13,Muted,true);
            InventorySlot(new Rect(76,462,240,78),inventory.EquippedStaff,"Nenhuma staff equipada");
            Label(new Rect(76,557,240,24),"GRIMÓRIO",13,Muted,true);
            InventorySlot(new Rect(76,585,240,78),inventory.EquippedGrimoire,"Sem grimório equipado");
            Box(new Rect(382,205,846,478),PanelColor); Label(new Rect(408,227,780,24),"MOCHILA  /  "+inventory.Items.Count+" ITENS",14,Accent,true);
            for (int index=0;index<inventory.Items.Count;index++)
            {
                RunItemInstance item=inventory.Items[index];
                if (!RunItemCatalog.TryGet(item.DefinitionId,out RunItemDefinition definition)) continue;
                float x=408+(index%4)*195,y=270+(index/4)*128;
                InventoryCard(new Rect(x,y,174,108),item,definition);
            }
            if (inventoryStatsOpen) InventoryStats();
            if (Button(new Rect(52,710,235,43),"Fechar  /  I")) mode=ScreenMode.Run;
            Label(new Rect(312,720,860,25),"E / BOTÃO INFERIOR: abrir baús e guardar itens   ·   I / VIEW: mochila",13,Muted);
        }
        void MagePortrait(Rect rect)
        {
            Box(rect,new Color(.17f,.12f,.27f));
            Sprite sprite=MageSprites.Facing(Vector2.down);
            if (sprite == null) { Label(new Rect(rect.x,rect.y+48,rect.width,35),"MAGO",24,White,true); return; }
            Rect source=sprite.rect;
            Rect texCoords=new Rect(source.x/sprite.texture.width,source.y/sprite.texture.height,source.width/sprite.texture.width,source.height/sprite.texture.height);
            GUI.DrawTextureWithTexCoords(rect,sprite.texture,texCoords,true);
        }
        void InventorySlot(Rect rect,RunItemInstance item,string empty)
        {
            Box(rect,new Color(.05f,.07f,.12f));
            if (item == null) { Label(new Rect(rect.x+12,rect.y+25,rect.width-24,24),empty,13,Muted); return; }
            if (!RunItemCatalog.TryGet(item.DefinitionId,out RunItemDefinition definition)) return;
            Box(new Rect(rect.x,rect.y,4,rect.height),ItemColor(definition));
            Label(new Rect(rect.x+14,rect.y+12,rect.width-22,25),definition.Label,14,White,true);
            Label(new Rect(rect.x+14,rect.y+42,rect.width-22,21),"NÍVEL "+item.Level+"/"+definition.MaxLevel,12,ItemColor(definition));
        }
        void InventoryCard(Rect rect,RunItemInstance item,RunItemDefinition definition)
        {
            bool equipped=item.InstanceId==inventory.EquippedStaffId || item.InstanceId==inventory.EquippedGrimoireId;
            Color color=ItemColor(definition); Box(rect,new Color(.06f,.08f,.14f)); Box(new Rect(rect.x,rect.y,rect.width,3),equipped?color:new Color(.2f,.25f,.34f));
            Label(new Rect(rect.x+12,rect.y+15,rect.width-24,42),definition.Label,14,color,true);
            Label(new Rect(rect.x+12,rect.y+64,rect.width-24,20),"NÍVEL "+item.Level+"/"+definition.MaxLevel,12,White);
            bool canEquip=definition.Kind==RunItemKind.Staff || definition.Kind==RunItemKind.Grimoire;
            if (Button(new Rect(rect.x+12,rect.y+84,rect.width-24,19),canEquip?(equipped?"Equipado":"Equipar"):"Detalhes",false))
            { if (canEquip) EquipInventoryItem(item); else selectedInventoryItemId=item.InstanceId; }
            bool hover=rect.Contains(Event.current.mousePosition);
            if (hover || selectedInventoryItemId==item.InstanceId) ItemPreview(new Rect(rect.x,rect.y+111,rect.width,85),item,definition);
        }
        void ItemPreview(Rect rect,RunItemInstance item,RunItemDefinition definition)
        {
            Box(rect,new Color(.02f,.025f,.05f,.98f));
            string effect=ItemEffect(definition,item.Level);
            if (Event.current.shift && definition.Kind != RunItemKind.Grimoire)
            {
                string levels="";
                for (int level=1;level<=definition.MaxLevel;level++) levels+=(level<=item.Level?ItemEffect(definition,level):"[ "+ItemEffect(definition,level)+" ]")+(level==definition.MaxLevel?"":"  ");
                effect="Por nível: "+levels;
            }
            Label(new Rect(rect.x+8,rect.y+7,rect.width-16,rect.height-12),effect,12,ItemColor(definition));
        }
        string ItemEffect(RunItemDefinition definition,int level)
        {
            if (definition.Kind==RunItemKind.Ring) return "+"+level+" ricochete"+(level==1?"":"s");
            if (definition.Kind==RunItemKind.Staff) return "+"+Mathf.RoundToInt((level-1)*12)+"% poder elemental";
            return MageEquipmentCatalog.TryGetGrimoire(definition.Id,out GrimoireDefinition grimoire)?"Libera this."+grimoire.BaseSpellId+"() para o mago.\nNão altera seu programa.":definition.Description;
        }
        void InventoryStats()
        {
            Rect rect=new Rect(247,208,360,235); Box(rect,new Color(.025f,.035f,.065f,.99f));
            Label(new Rect(267,225,320,25),"STATUS DO MAGO",16,Accent,true);
            Label(new Rect(267,262,320,138),"Nível "+level+"\nVida "+Mathf.CeilToInt(hp)+" / "+Mathf.CeilToInt(MaxHealth)+"\nEnergia "+energy+" / "+MaxEnergy+"\nMemória "+Budget+"\nVelocidade +"+Mathf.RoundToInt(speedBonus*100)+"%\nPoder +"+Mathf.RoundToInt(damageBonus*100)+"%\nRicochetes "+inventory.RicochetCount,14,White);
        }
        void SaveProgramUI()
        {
            Box(new Rect(0,0,1280,800),new Color(.015f,.02f,.04f,.94f));
            Header("FIM DA TENTATIVA","GUARDAR UMA CÓPIA DO PROGRAMA?","O código já está salvo no personagem. Você também pode nomear uma cópia na Biblioteca.");
            Box(new Rect(350,255,580,255),PanelColor);
            Label(new Rect(385,285,510,28),"Programa do Mago Arcanista",18,Accent,true);
            Label(new Rect(385,328,510,22),"Nome para a Biblioteca",13,Muted);
            programSaveName=GUI.TextField(new Rect(385,357,510,38),programSaveName,48);
            if (Button(new Rect(385,425,246,47),"Guardar cópia",true)) SaveCharacterProgram();
            if (Button(new Rect(649,425,246,47),"Continuar",false)) SkipSaveProgram();
        }
        void ValidateDraft()
        { draftResult=SpellCompiler.Compile(draft,Options()); highlightedSource=""; }
        DraftEdit CaptureDraft()
        { return new DraftEdit { Text=draft,Cursor=Mathf.Clamp(caret,0,draft.Length),Selection=Mathf.Clamp(selectionCaret,0,draft.Length) }; }
        void PushDraftEdit(List<DraftEdit> history,DraftEdit edit)
        {
            if (history.Count==DraftHistoryLimit) history.RemoveAt(0);
            history.Add(edit);
        }
        void ResetDraftHistory()
        {
            draftUndo.Clear(); draftRedo.Clear(); caret=selectionCaret=0;
            completionVisible=false; focusCode=true;
        }
        void ChangeDraft(string text,int cursor,int selection)
        {
            if (text==draft) return;
            PushDraftEdit(draftUndo,CaptureDraft()); draftRedo.Clear();
            draft=text; caret=Mathf.Clamp(cursor,0,draft.Length); selectionCaret=Mathf.Clamp(selection,0,draft.Length);
            completionVisible=false; focusCode=true; ValidateDraft();
        }
        void RestoreDraftEdit(List<DraftEdit> from,List<DraftEdit> to)
        {
            if (from.Count==0) return;
            PushDraftEdit(to,CaptureDraft());
            DraftEdit edit=from[from.Count-1]; from.RemoveAt(from.Count-1);
            draft=edit.Text; caret=edit.Cursor; selectionCaret=edit.Selection;
            completionVisible=false; focusCode=true; ValidateDraft();
            editorMessage="Rascunho alterado. Aplique o programa para usar as mudanças.";
        }
        bool HandleDraftHistoryShortcut(Event input)
        {
            if (!(input.control || input.command) || input.alt) return false;
            if (input.keyCode==KeyCode.Z)
            {
                if (input.shift) RestoreDraftEdit(draftRedo,draftUndo); else RestoreDraftEdit(draftUndo,draftRedo);
                return true;
            }
            if (input.keyCode==KeyCode.Y) { RestoreDraftEdit(draftRedo,draftUndo); return true; }
            return false;
        }
        string Highlight(string code)
        {
            return Regex.Replace(code,@"//[^\n]*|\b(?:class|extends|void|int|float|bool|var|if|else|for|return|this|true|false)\b|\b(?:Fireball|Icebolt|FlameWave|FrostNova|MagoArcanista|Mago)\b|\b\d+(?:\.\d+)?\b",m=>
            {
                string value=m.Value;
                string color=value.StartsWith("//")?"#647B8D":char.IsDigit(value[0])?"#E6B974":char.IsUpper(value[0])?"#7ADFD3":"#C3A0EF";
                return "<color="+color+">"+value+"</color>";
            });
        }
        void InsertCode(string snippet)
        {
            int position=Mathf.Clamp(caret,0,draft.Length);
            ChangeDraft(draft.Insert(position,snippet),position+snippet.Length,position+snippet.Length);
        }
        void EditorUI()
        {
            Box(new Rect(0,0,1280,800),Background);
            Label(new Rect(30,22,850,24),"PROGRAMA DO PERSONAGEM  /  MAGO ARCANO  /  "+(dungeon==null?"SANTUÁRIO":"TENTATIVA ATUAL"),14,Accent,true);
            Label(new Rect(28,57,820,46),"Magia é uma questão de lógica.",31,White,true);
            ControllerHint(new Rect(28,91,840,22),"CONTROLE: navega ações e exemplos. Para escrever código, use teclado ou mouse.");
            if (Button(new Rect(1030,34,217,42),"Voltar ao "+(dungeon==null?"santuário":"jogo"))) CloseEditor();
            Box(new Rect(28,117,840,516),new Color(.025f,.035f,.057f));
            Box(new Rect(28,117,840,35),PanelColor);
            Label(new Rect(43,123,580,25),"MagoArcanista.arc",14,Accent);
            string status=draftResult!=null&&draftResult.Success?draftResult.Cost+" / "+Budget+" pontos":"Código não aplicável";
            Label(new Rect(625,123,225,25),status,13,draftResult!=null&&draftResult.Success?new Color(.43f,.9f,.71f):new Color(1,.52f,.51f));
            string[] lines=draft.Split('\n');
            float lineHeight=codeStyle.lineHeight>0?codeStyle.lineHeight:20;
            float contentWidth=Mathf.Max(775,lines.Max(l=>codeStyle.CalcSize(new GUIContent(l)).x)+70);
            float contentHeight=Mathf.Max(445,lines.Length*lineHeight+30);
            codeScroll=GUI.BeginScrollView(new Rect(28,156,840,476),codeScroll,new Rect(0,0,contentWidth,contentHeight));
            for (int i=0;i<lines.Length;i++) Label(new Rect(5,4+i*lineHeight,40,lineHeight),(i+1).ToString(),13,Muted);
            Rect codeRect=new Rect(48,0,contentWidth-55,contentHeight);
            HandleEditorKeys();
            if (!focusCode && GUI.GetNameOfFocusedControl()=="SpellCode")
            {
                var before=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
                caret=before.cursorIndex; selectionCaret=before.selectIndex;
            }
            // Transparent input draws selection/caret; highlighted text is rendered on top at identical metrics.
            GUI.SetNextControlName("SpellCode");
            // Give the field focus before it handles this GUI event. Doing it afterwards lost
            // keyboard focus after Enter on some Unity IMGUI passes.
            if (focusCode) GUI.FocusControl("SpellCode");
            string edited=GUI.TextArea(codeRect,draft,12000,codeInputStyle);
            if (edited!=draft)
            {
                var after=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
                ChangeDraft(edited,after.cursorIndex,after.selectIndex); focusCode=false;
            }
            if (highlightedSource!=draft) { highlighted=Highlight(draft); highlightedSource=draft; }
            GUI.Label(codeRect,highlighted,codeStyle);
            if (GUI.GetNameOfFocusedControl()=="SpellCode")
            {
                var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
                if (!focusCode) { caret=editor.cursorIndex; selectionCaret=editor.selectIndex; }
            }
            RefreshCompletions();
            CompletionPopup(lineHeight,contentWidth);
            if (focusCode && GUI.GetNameOfFocusedControl()=="SpellCode")
            {
                var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
                editor.text=draft; editor.cursorIndex=caret; editor.selectIndex=selectionCaret; focusCode=false;
            }
            GUI.EndScrollView();
            Box(new Rect(28,646,840,81),PanelColor);
            string message=draftResult!=null&&!draftResult.Success?draftResult.Error.ToString():editorMessage;
            Label(new Rect(43,658,810,57),message,14,draftResult!=null&&!draftResult.Success?new Color(1,.6f,.55f):Muted);
            if (Button(new Rect(28,744,233,39),"Aplicar programa",true,draftResult!=null&&draftResult.Success)) ApplyDraft();
            if (Button(new Rect(276,744,185,39),"Código do personagem")) ChangeDraft(source,0,0);
            if (Button(new Rect(476,744,184,39),"Ataque básico")) ChangeDraft(SpellCompiler.Starter(MageClassName,PrimarySpellId),0,0);
            if (Button(new Rect(675,744,193,39),"Exemplo: carga")) ChangeDraft(SpellCompiler.Charged(MageClassName,PrimarySpellId),0,0);
            DocumentationUI();
        }
        void CompleteAtCaret()
        {
            RefreshCompletions();
            if (!completionVisible) { editorMessage="Nenhuma sugestão neste ponto."; return; }
            AcceptCompletion();
        }
        int CompletionStart()
        {
            caret=Mathf.Clamp(caret,0,draft.Length); int start=caret;
            while (start>0&&(char.IsLetterOrDigit(draft[start-1])||draft[start-1]=='_')) start--;
            return start;
        }
        void RefreshCompletions()
        {
            completions.Clear(); completionVisible=false;
            if (string.IsNullOrEmpty(draft)) return;
            int start=CompletionStart();
            if (start<5 || draft.Substring(start-5,5)!="this.") return;
            string prefix=draft.Substring(start,caret-start);
            var known=new HashSet<string>();
            Action<string,string,string> add=(label,insert,detail)=>
            {
                if (label.StartsWith(prefix,StringComparison.Ordinal)&&known.Add(label)) completions.Add(new Completion {Label=label,Insert=insert,Detail=detail});
            };
            foreach (string spell in unlocked.OrderBy(s=>s)) add(spell+"()",spell+"()","magia do kernel");
            add("charge(1)","charge(1)","acumula energia");
            if (speedCastUnlocked) add("speedCast(1)","speedCast(1)","+20% velocidade de conjuração");
            add("energia","energia","energia atual"); add("vida","vida","vida atual"); add("inimigos","inimigos","inimigos na sala");
            foreach (Match match in Regex.Matches(draft,@"(?m)^\s*(?:void|int|float|bool|Fireball|FlameWave|Icebolt|FrostNova)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(\s*\)"))
            {
                string method=match.Groups[1].Value;
                if (method!=SpellCompiler.EntryMethod) add(method+"()",method+"()","método do seu mago");
            }
            completionIndex=Mathf.Clamp(completionIndex,0,Mathf.Max(0,completions.Count-1));
            completionVisible=completions.Count>0;
        }
        void AcceptCompletion()
        {
            if (!completionVisible || completions.Count==0) return;
            int start=CompletionStart(); Completion completion=completions[completionIndex];
            ChangeDraft(draft.Remove(start,caret-start).Insert(start,completion.Insert),start+completion.Insert.Length,start+completion.Insert.Length);
            editorMessage="Inserido: this."+completion.Label;
        }
        void HandleEditorKeys()
        {
            if (GUI.GetNameOfFocusedControl()!="SpellCode" || Event.current.type!=EventType.KeyDown) return;
            var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
            caret=editor.cursorIndex; selectionCaret=editor.selectIndex;
            if (HandleDraftHistoryShortcut(Event.current))
            {
                editor.text=draft; editor.cursorIndex=caret; editor.selectIndex=selectionCaret;
                Event.current.Use(); return;
            }
            if (Event.current.control && Event.current.keyCode==KeyCode.Space) { CompleteAtCaret(); Event.current.Use(); return; }
            if (Event.current.keyCode==KeyCode.Tab)
            {
                RefreshCompletions();
                if (completionVisible && !Event.current.shift) AcceptCompletion(); else IndentSelection(Event.current.shift);
                Event.current.Use(); return;
            }
            if (completionVisible && (Event.current.keyCode==KeyCode.DownArrow || Event.current.keyCode==KeyCode.UpArrow))
            {
                int change=Event.current.keyCode==KeyCode.DownArrow?1:-1;
                completionIndex=(completionIndex+change+completions.Count)%completions.Count; Event.current.Use(); return;
            }
            if (completionVisible && Event.current.keyCode==KeyCode.Escape) { completionVisible=false; Event.current.Use(); return; }
            if (Event.current.keyCode==KeyCode.Return || Event.current.keyCode==KeyCode.KeypadEnter)
            { InsertIndentedNewline(); Event.current.Use(); return; }
        }
        void IndentSelection(bool outdent)
        {
            // Do not trust TextEditor.selectIndex here: it can temporarily point to zero
            // when IMGUI restores focus, which made Tab appear to select/indent the whole file.
            int position=Mathf.Clamp(caret,0,draft.Length);
            if (!outdent) { ChangeDraft(draft.Insert(position,"    "),position+4,position+4); return; }
            int line=draft.LastIndexOf('\n',Mathf.Max(0,position-1))+1;
            int amount=0;
            if (line<draft.Length && draft[line]=='\t') amount=1;
            else while (amount<4 && line+amount<draft.Length && draft[line+amount]==' ') amount++;
            if (amount>0 && position>=line+amount) ChangeDraft(draft.Remove(line,amount),position-amount,position-amount);
        }
        void InsertIndentedNewline()
        {
            int cursor=Mathf.Clamp(caret,0,draft.Length);
            int line=draft.LastIndexOf('\n',Mathf.Max(0,cursor-1))+1;
            int indentEnd=line; while (indentEnd<draft.Length&&(draft[indentEnd]==' '||draft[indentEnd]=='\t')) indentEnd++;
            string indent=draft.Substring(line,indentEnd-line);
            if (draft.Substring(line,cursor-line).TrimEnd().EndsWith("{")) indent+="    ";
            ChangeDraft(draft.Insert(cursor,"\n"+indent),cursor+1+indent.Length,cursor+1+indent.Length);
        }
        void CompletionPopup(float lineHeight,float contentWidth)
        {
            if (!completionVisible) return;
            int lineStart=draft.LastIndexOf('\n',Mathf.Max(0,caret-1))+1;
            int line=0; for (int i=0;i<lineStart;i++) if (draft[i]=='\n') line++;
            float before=codeStyle.CalcSize(new GUIContent(draft.Substring(lineStart,caret-lineStart))).x;
            float x=Mathf.Clamp(48+before,48,contentWidth-260), y=4+(line+1)*lineHeight;
            int count=Mathf.Min(6,completions.Count); Rect popup=new Rect(x,y,250,count*27+6);
            Box(popup,new Color(.08f,.11f,.18f,.99f));
            for (int i=0;i<count;i++)
            {
                Rect row=new Rect(x+3,y+3+i*27,244,24); if (i==completionIndex) Box(row,new Color(.18f,.31f,.43f));
                if (GUI.Button(row,completions[i].Label+"   "+completions[i].Detail,smallStyle)) { completionIndex=i; AcceptCompletion(); }
            }
        }
        void DocumentationUI()
        {
            Box(new Rect(889,117,359,666),PanelColor);
            Label(new Rect(908,133,320,25),"KERNEL / REFERÊNCIA",14,Accent,true);
            docsScroll=GUI.BeginScrollView(new Rect(904,177,331,590),docsScroll,new Rect(0,0,307,1190));
            Label(new Rect(0,0,302,102),"Digite this. para ver sugestões.\nTAB aceita/indenta; Shift+TAB remove.\nEnter preserva a indentação.\nCtrl+Z desfaz.\nCtrl+Shift+Z / Ctrl+Y refaz.",14,Muted);
            float y=122;
            foreach (string spell in unlocked.OrderBy(s=>s))
            {
                string type=SpellCompiler.SpellTypes[spell];
                Label(new Rect(0,y,300,25),type+" "+spell+"()",16,Accent,true);
                Label(new Rect(0,y+28,300,43),"Cria a magia. Use .cast() para lançar.\nCusto: 2 pontos por chamada.",13,Muted);
                if (Button(new Rect(0,y+76,300,35),"Inserir "+spell+"()")) InsertCode("this."+spell+"().cast();\n");
                y+=131;
            }
            Label(new Rect(0,y,300,25),"void charge(int unidades)",16,Accent,true);
            Label(new Rect(0,y+30,300,69),"Acumula 1 a 10 unidades: 0,35 s cada.\nO disparo consome toda a energia.\nCusto: 1 ponto. +25% de dano/unidade.",13,Muted);
            if (Button(new Rect(0,y+103,300,35),"Inserir carregamento")) InsertCode("this.charge(1);\n"); y+=159;
            if (speedCastUnlocked)
            {
                Label(new Rect(0,y,300,25),"void speedCast(int nivel)",16,Accent,true);
                Label(new Rect(0,y+30,300,64),"Acelera charge e cast em 20% por nível.\nNível 1 custa 3 pontos; nível 2 custa 6.\nUse inteiro literal de 1 a 10.",13,Muted);
                if (Button(new Rect(0,y+98,300,35),"Inserir speedCast(1)")) InsertCode("this.speedCast(1);\n"); y+=154;
            }
            else { Label(new Rect(0,y,300,48),"speedCast() está bloqueado.\nDesbloqueie Conjuração célere no santuário.",13,Muted); y+=66; }
            Label(new Rect(0,y,300,25),"if / else · 2 pontos",16,Accent,true);
            if (Button(new Rect(0,y+33,300,35),"Inserir condição")) InsertCode("if (this.energia >= 3) {\n    \n}\n"); y+=92;
            Label(new Rect(0,y,300,25),"for · 3 pontos",16,Accent,true);
            Label(new Rect(0,y+30,300,46),"1 a 10 repetições. Até 2 loops aninhados.\nO corpo conta uma vez no orçamento.",13,Muted);
            if (Button(new Rect(0,y+82,300,35),"Inserir loop")) InsertCode("for (int i = 0; i < 3; i++) {\n    this.charge(1);\n}\n"); y+=139;
            Label(new Rect(0,y,300,25),"Estado do mago",16,Accent,true);
            Label(new Rect(0,y+31,300,112),"int this.energia\nfloat this.vida\nint this.inimigos\n\nMétodos: Fireball forte() {\n    return this.fireball();\n}",14,Muted);
            GUI.EndScrollView();
        }
        void PauseUI()
        {
            Box(new Rect(0,0,1280,800),new Color(.02f,.025f,.05f,.87f)); Box(new Rect(395,205,490,400),PanelColor);
            Label(new Rect(430,237,430,60),"Execução pausada.",30,White,true);
            if (Button(new Rect(430,317,420,49),"Continuar",true)) mode=ScreenMode.Run;
            if (Button(new Rect(430,384,420,49),"Restaurar ataque básico"))
                RestoreBasicAttack();
            if (Button(new Rect(430,452,420,49),"Encerrar tentativa e voltar à base")) FinishRun(false);
            Label(new Rect(430,526,420,47),"Os fragmentos coletados serão depositados.\nNíveis e melhorias da tentativa serão reiniciados.",14,Muted);
            ControllerHint(new Rect(430,570,420,25),"BOTÃO INFERIOR: confirmar  ·  BOTÃO DIREITO ou MENU: continuar");
        }
        void ResultUI()
        {
            Box(new Rect(0,0,1280,800),Background);
            Header(won?"DUNGEON CONCLUÍDA":"TENTATIVA ENCERRADA",resultTitle,"Seu legado permanece. O próximo programa começa com uma nova ideia.");
            string[] titles={"FRAGMENTOS","NÍVEL ALCANÇADO","TEMPO"};
            string[] values={"+"+runCoins,level.ToString(),TimeSpan.FromSeconds(elapsed).ToString(@"mm\:ss")};
            for (int i=0;i<3;i++)
            {
                float x=52+i*397; Box(new Rect(x,259,376,195),PanelColor);
                Label(new Rect(x+25,284,325,30),titles[i],14,Muted,true); Label(new Rect(x+25,338,325,78),values[i],51,Accent,true);
            }
            Label(new Rect(52,491,1100,65),"Invista seus fragmentos na base para ampliar vida, energia e complexidade,\nou desbloqueie uma nova magia para suas próximas tentativas.",20,Muted);
            if (Button(new Rect(52,595,367,58),"Voltar ao santuário →",true)) PrepareHub();
            if (saveDirty && Button(new Rect(443,595,310,58),"Tentar salvar novamente")) SaveProfile();
            ControllerHint(new Rect(52,675,500,30),"BOTÃO INFERIOR ou DIREITO: voltar ao santuário");
        }
    }
}
