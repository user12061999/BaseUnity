using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor
{
    public static class GameplayFoundationBuilder
    {
        private static Font font;
        [MenuItem("Tools/BaseUnity/Game Flow/Prepare Gameplay Foundations")]
        public static void GenerateSelected() { Generate(UIThemeBuilder.SelectedConfig()); }
        public static void Generate(GameFlowConfig config)
        {
            if (!config || !AssetDatabase.GetAssetPath(config).StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidOperationException("Choose a project-owned GameFlowConfig");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first");
            string folder = UIThemeBuilder.Folder(config), backup = UIThemeBuilder.Backup(config);
            Directory.CreateDirectory(folder + "/GameplayFoundations"); AssetDatabase.Refresh();
            string path = folder + "/GameplayFoundations/";
            font = config.uiPrefab && config.uiPrefab.title ? config.uiPrefab.title.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var label = AssetDatabase.LoadAssetAtPath<Text>(path + "FloatingText.prefab");
            if (!label) { var item = Label(null,"Floating text", "+10",0,0,280,80,46); try { label = PrefabUtility.SaveAsPrefabAsset(item.gameObject,path+"FloatingText.prefab").GetComponent<Text>(); } finally { UnityEngine.Object.DestroyImmediate(item.gameObject); } }
            if (!AssetDatabase.LoadAssetAtPath<InGameBadgeView>(path+"NotificationBadge.prefab"))
            {
                var root=Rect(null,"Notification badge",60,60,0,0);
                try { var badge=root.gameObject.AddComponent<InGameBadgeView>(); var dot=Box(root,"Dot",60,60,0,0); dot.GetComponent<Image>().color=Color.red; dot.GetComponent<Image>().raycastTarget=false; badge.dot=dot.gameObject; badge.count=Label(dot,"Count","1",0,0,55,55,24); PrefabUtility.SaveAsPrefabAsset(root.gameObject,path+"NotificationBadge.prefab"); }
                finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
            }
            if (!config.foundationPrefab)
            {
                config.foundationPrefab = AssetDatabase.LoadAssetAtPath<GameplayFoundationView>(path+"FoundationMenu.prefab");
                if (!AssetDatabase.LoadAssetAtPath<InGameBadgeView>(path+"NotificationBadge.prefab"))
            {
                var root=Rect(null,"Notification badge",60,60,0,0);
                try { var badge=root.gameObject.AddComponent<InGameBadgeView>(); var dot=Box(root,"Dot",60,60,0,0); dot.GetComponent<Image>().color=Color.red; dot.GetComponent<Image>().raycastTarget=false; badge.dot=dot.gameObject; badge.count=Label(dot,"Count","1",0,0,55,55,24); PrefabUtility.SaveAsPrefabAsset(root.gameObject,path+"NotificationBadge.prefab"); }
                finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
            }
            if (!config.foundationPrefab)
                {
                    var root = Canvas("Gameplay foundations",140);
                    try
                    {
                        var view = root.gameObject.AddComponent<GameplayFoundationView>(); var safe = Rect(root,"Safe Area",900,1600,0,0); var area = root.gameObject.AddComponent<GameFlowSafeArea>(); area.canvas=root.GetComponent<Canvas>(); area.content=safe;
                        view.home = Rect(safe,"Home additions",900,1600,0,0).gameObject;
                        view.open=Button(view.home.transform,"Gameplay menu","GIFTS / PROFILE",-230,650,390,75);
                        view.status=Label(view.home.transform,"Energy and XP","ENERGY 5/5  PLAYER LV 1",210,650,420,90,22);
                        view.badge=Label(view.open.transform,"Badge","2",170,24,55,55,28); view.badge.color=Color.yellow;
                        var modal=Rect(safe,"Gameplay menu modal",900,1600,0,0); view.modal=modal.gameObject;
                        var block=Box(modal,"Raycast blocker",4000,4000,0,0); block.GetComponent<Image>().color=new Color(0,0,0,.7f);
                        var panel=Box(modal,"Panel",840,1300,0,0);
                        view.title=Label(panel,"Title","GIFTS",0,550,780,80,44);
                        view.gifts=Button(panel,"Gifts","GIFTS",-270,440,245,85); view.achievements=Button(panel,"Achievements","ACHIEVEMENTS",0,440,265,85); view.settings=Button(panel,"Advanced Settings","SETTINGS",270,440,245,85);
                        view.rows=new Button[6]; view.rowLabels=new Text[6];
                        for(int i=0;i<6;i++) { view.rows[i]=Button(panel,"Action "+i,"Action",0,300-i*120,760,95); view.rowLabels[i]=view.rows[i].GetComponentInChildren<Text>(); }
                        view.message=Label(panel,"Message","",0,-420,760,90,22);
                        view.previous=Button(panel,"Previous","<",-300,-500,100,80); view.next=Button(panel,"Next",">",300,-500,100,80); view.pageNumber=Label(panel,"Page","1 / 1",0,-500,300,70,24);
                        view.close=Button(panel,"Close","CLOSE",0,-600,400,75);
                        var effectRoot=Rect(root,"Gameplay effects",900,1600,0,0); Stretch(effectRoot); view.effects=effectRoot.gameObject.AddComponent<GameplayEffects>(); view.effects.floatingTextPrefab=label;
                        modal.gameObject.SetActive(false);
                        config.foundationPrefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path+"FoundationMenu.prefab").GetComponent<GameplayFoundationView>();
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
                }
            }
            var joystick=AssetDatabase.LoadAssetAtPath<MobileJoystick>(path+"Joystick.prefab");
            if(!joystick) { var root=Box(null,"Joystick",220,220,0,0); try { joystick=root.gameObject.AddComponent<MobileJoystick>(); joystick.area=root; joystick.handle=Box(root,"Handle",85,85,0,0); joystick.handle.GetComponent<Image>().raycastTarget=false; joystick.handle.GetComponent<Image>().color=new Color(.25f,.6f,1); joystick=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path+"Joystick.prefab").GetComponent<MobileJoystick>(); } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); } }
            var action=AssetDatabase.LoadAssetAtPath<MobileActionButton>(path+"HoldButton.prefab");
            if(!action) { var root=Box(null,"Hold action",160,160,0,0); try { action=root.gameObject.AddComponent<MobileActionButton>(); Label(root,"Caption","FIRE",0,0,150,80,30); action=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path+"HoldButton.prefab").GetComponent<MobileActionButton>(); } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); } }
            var swipe=AssetDatabase.LoadAssetAtPath<MobileSwipeSurface>(path+"SwipeSurface.prefab");
            if(!swipe) { var root=Box(null,"Swipe surface",700,150,0,0); try { swipe=root.gameObject.AddComponent<MobileSwipeSurface>(); Label(root,"Caption","SWIPE HERE",0,0,650,90,28); swipe=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path+"SwipeSurface.prefab").GetComponent<MobileSwipeSurface>(); } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); } }
            var interactables=new GameplayInteractable[3]; string[] names={"TapObject","Collectible","Draggable"};
            for(int i=0;i<3;i++) { var item=AssetDatabase.LoadAssetAtPath<GameplayInteractable>(path+names[i]+".prefab"); if(!item) { var root=Box(null,names[i],210,130,0,0); try { item=root.gameObject.AddComponent<GameplayInteractable>(); item.mode=(GameplayInteractionMode)i; var hit=root.gameObject.AddComponent<GameplayHitFeedback>(); hit.graphic=root.GetComponent<Image>(); hit.target=root; Label(root,"Caption",names[i].ToUpperInvariant(),0,0,200,100,24); item=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path+names[i]+".prefab").GetComponent<GameplayInteractable>(); } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); } } interactables[i]=item; }
            var zone=AssetDatabase.LoadAssetAtPath<GameplayDropZone>(path+"DropZone.prefab");
            if(!zone) { var root=Box(null,"Drop zone",300,160,0,0); try { zone=root.gameObject.AddComponent<GameplayDropZone>(); Label(root,"Caption","DROP HERE",0,0,280,100,26); zone=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path+"DropZone.prefab").GetComponent<GameplayDropZone>(); } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); } }
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(path+"GameplayPlayground.prefab"))
            {
                var root=Canvas("Gameplay playground",10);
                try { var demo=root.gameObject.AddComponent<GameplayPlayground>(); var safe=Rect(root,"Safe Area",900,1600,0,0); var area=root.gameObject.AddComponent<GameFlowSafeArea>(); area.canvas=root.GetComponent<Canvas>(); area.content=safe;
                    var bg=Box(root,"Background",0,0,0,0); Stretch(bg); bg.SetAsFirstSibling(); bg.GetComponent<Image>().raycastTarget=false; bg.GetComponent<Image>().color=new Color(.02f,.04f,.08f);
                    demo.status=Label(safe,"Status","TAP / COLLECT / DRAG / SWIPE",0,420,820,100,28);
                    demo.tap=UnityEngine.Object.Instantiate(interactables[0],safe); ((RectTransform)demo.tap.transform).anchoredPosition=new Vector2(-230,240);
                    demo.collectible=UnityEngine.Object.Instantiate(interactables[1],safe); ((RectTransform)demo.collectible.transform).anchoredPosition=new Vector2(230,240);
                    demo.draggable=UnityEngine.Object.Instantiate(interactables[2],safe); ((RectTransform)demo.draggable.transform).anchoredPosition=new Vector2(-230,20);
                    demo.dropZone=UnityEngine.Object.Instantiate(zone,safe); ((RectTransform)demo.dropZone.transform).anchoredPosition=new Vector2(230,20);
                    demo.swipe=UnityEngine.Object.Instantiate(swipe,safe); ((RectTransform)demo.swipe.transform).anchoredPosition=new Vector2(0,-200);
                    var stick=UnityEngine.Object.Instantiate(joystick,safe); ((RectTransform)stick.transform).anchoredPosition=new Vector2(-260,-530);
                    var fire=UnityEngine.Object.Instantiate(action,safe); ((RectTransform)fire.transform).anchoredPosition=new Vector2(280,-530);
                    demo.actor=Box(safe,"Controlled object",65,65,0,-430); demo.actor.GetComponent<Image>().raycastTarget=false; demo.actor.GetComponent<Image>().color=Color.yellow;
                    var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path+"GameplayPlayground.prefab");
                    string levelPath=path+"GameplayDemoLevel.asset"; if(!AssetDatabase.LoadAssetAtPath<LevelData>(levelPath)) { var level=ScriptableObject.CreateInstance<LevelData>(); level.title="Gameplay foundations demo"; level.targetScore=100; level.timeLimitSeconds=120; level.gameplayPrefab=prefab; AssetDatabase.CreateAsset(level,levelPath); }
                } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
            }
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("Gameplay foundation prefabs ready at "+path+". Existing assets preserved. Backup="+backup);
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static RectTransform Canvas(string name,int order) { var rect=Rect(null,name,900,1600,0,0); var canvas=rect.gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=order; rect.gameObject.AddComponent<GraphicRaycaster>(); var scaler=rect.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(900,1600); scaler.matchWidthOrHeight=.5f; return rect; }
        private static RectTransform Rect(Transform parent,string name,float w,float h,float x,float y) { var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); if(parent) rect.SetParent(parent,false); rect.sizeDelta=new Vector2(w,h); rect.anchoredPosition=new Vector2(x,y); return rect; }
        private static RectTransform Box(Transform parent,string name,float w,float h,float x,float y) { var rect=Rect(parent,name,w,h,x,y); rect.gameObject.AddComponent<Image>().color=new Color(.07f,.17f,.28f); return rect; }
        private static Text Label(Transform parent,string name,string value,float x,float y,float w,float h,int size) { var rect=Rect(parent,name,w,h,x,y); var text=rect.gameObject.AddComponent<Text>(); text.font=font; text.text=value; text.fontSize=size; text.color=Color.white; text.alignment=TextAnchor.MiddleCenter; text.raycastTarget=false; return text; }
        private static Button Button(Transform parent,string name,string value,float x,float y,float w,float h) { var rect=Box(parent,name,w,h,x,y); rect.GetComponent<Image>().color=new Color(.12f,.35f,.55f); rect.GetComponent<Image>().type=Image.Type.Sliced; var button=rect.gameObject.AddComponent<Button>(); Label(rect,"Label",value,0,0,w-24,h-12,24); return button; }
    }
}
