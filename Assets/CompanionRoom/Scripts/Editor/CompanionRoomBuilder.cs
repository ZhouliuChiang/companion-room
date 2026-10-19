using System;
using System.IO;
using System.Linq;
using CompanionRoom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CompanionRoom.EditorTools
{
    /// <summary>
    /// Builds Assets/CompanionRoom/Scenes/CompanionRoom.unity: the pink room with placeholder
    /// furniture, the six clickable items (相片, 電腦, 壁燈, 窗戶, 塔羅, 櫃子) plus 凡安, lights,
    /// camera and post-processing. Every item keeps its look under a child named "Model" so real
    /// models can replace the placeholders without touching the wiring.
    /// Units: metres. Floor top at y = 0; back wall inner face at z = 2.06; left wall inner face at x = -2.56.
    /// </summary>
    public static class CompanionRoomBuilder
    {
        public const string Root = "Assets/CompanionRoom";
        public const string ScenePath = Root + "/Scenes/CompanionRoom.unity";
        const string SettingsFolder = Root + "/Settings";
        const string SkinPath = SettingsFolder + "/RoomUISkin.asset";
        const string VolumePath = SettingsFolder + "/RoomVolume.asset";
        const string OutlineMaterialPath = Root + "/Resources/" + HoverOutline.MaterialResource + ".mat";
        const string ChibiPrefab = "Assets/ChibiCompanion/ChibiCompanion.prefab";

        const float BackWall = 2.06f;
        const float LeftWall = -2.56f;
        const float WallHeight = 2.7f;

        static PlaceholderKit kit;

        [MenuItem("Tools/兔兔房間/1. 建立範例場景 (Build Room Scene)", priority = 1)]
        static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
            EditorUtility.DisplayDialog("兔兔房間", "場景已建立：\n" + ScenePath, "好");
        }

        /// <summary>Batch mode entry: -executeMethod CompanionRoom.EditorTools.CompanionRoomBuilder.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("[CompanionRoom] Scene built: " + ScenePath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static Scene Build()
        {
            // Once the user's model workspace exists, preserve their layout and never recreate
            // the retired placeholder room or character through the old build commands.
            if (File.Exists(UserModelWorkspace.ManifestPath) && File.Exists(ScenePath))
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(SettingsFolder);
            EnsureCompanionLayer();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            kit = new PlaceholderKit();
            PaletteSetup();

            var room = new GameObject("Room").transform;
            BuildShell(room);
            BuildDecor(room);

            var items = new GameObject("Items").transform;
            BuildWindow(items, out var skyRenderer);
            BuildTarot(items);
            BuildPhoto(items);
            BuildLamp(items);
            BuildComputer(items);
            BuildCabinet(items);

            var camera = BuildCamera();
            var companion = BuildCompanion(camera.transform);
            var volume = BuildVolume();
            var lighting = BuildLighting(camera, volume, skyRenderer);
            BuildApp(camera, companion, lighting);

            foreach (var item in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoomItem>(true)))
                item.FitCollider();

            kit.Save();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            return scene;
        }

        // ------------------------------------------------------------------ palette

        static Material wall, trim, floor, floorBase, cream, pink, pinkDeep, lavender, cloth, gold, wood, green, greenDark, pot, rugCream, rugPink, white, mint, cardFace, cardBack, inner;

        static void PaletteSetup()
        {
            var planks = PlaceholderKit.FloorPlanks();
            var paper = PlaceholderKit.Wallpaper();
            wall = kit.Mat("Wall", "#FFFFFF", 0.05f, 0f, paper, new Vector2(6f, 3f));
            trim = kit.Mat("Trim", "#FFF3EA", 0.2f);
            floor = kit.Mat("FloorPlanks", "#FFFFFF", 0.25f, 0f, planks, new Vector2(3f, 3f));
            floorBase = kit.Mat("FloorBase", "#E5BE95", 0.2f);
            cream = kit.Mat("CreamWhite", "#FFF5EE", 0.22f);
            pink = kit.Mat("Pink", "#F4B5C1", 0.2f);
            pinkDeep = kit.Mat("PinkDeep", "#E996AB", 0.2f);
            lavender = kit.Mat("Lavender", "#CDB9E6", 0.75f);
            cloth = kit.Mat("TarotCloth", "#7D66A8", 0.1f);
            gold = kit.Mat("Gold", "#DDB872", 0.55f, 0.6f);
            wood = kit.Mat("WoodLight", "#D9B48C", 0.25f);
            green = kit.Mat("Leaf", "#9CC78C", 0.2f);
            greenDark = kit.Mat("LeafDark", "#7BAE6E", 0.2f);
            pot = kit.Mat("Pot", "#FBF2EC", 0.3f);
            rugCream = kit.Mat("RugCream", "#FFF8F3", 0.05f);
            rugPink = kit.Mat("RugPink", "#F6C3CF", 0.05f);
            white = kit.Mat("White", "#FFFFFF", 0.25f);
            mint = kit.Mat("Mint", "#BFE3D2", 0.2f);
            cardFace = kit.Mat("CardFace", "#FFF8EC", 0.2f);
            cardBack = kit.Mat("CardBack", "#6F5B9C", 0.3f);
            inner = kit.Mat("CabinetInside", "#E9C9D6", 0.1f);
        }

        // ------------------------------------------------------------------ room shell

        static void BuildShell(Transform room)
        {
            var shell = Group("Shell", room);
            kit.Box("FloorBase", shell, new Vector3(0f, -0.2f, 0f), new Vector3(5.62f, 0.14f, 4.62f), floorBase, 0.06f);
            kit.Box("Floor", shell, new Vector3(0f, -0.07f, 0f), new Vector3(5.4f, 0.14f, 4.4f), floor, 0.04f);

            // Back wall.
            kit.Box("BackWall", shell, new Vector3(0f, WallHeight * 0.5f, BackWall + 0.07f), new Vector3(5.4f, WallHeight, 0.14f), wall, 0f);
            // Left wall, built around the window opening (z -0.45..0.85, y 0.95..2.15).
            float x = LeftWall - 0.07f;
            kit.Box("LeftWall_Low", shell, new Vector3(x, 0.475f, -0.07f), new Vector3(0.14f, 0.95f, 4.26f), wall, 0f);
            kit.Box("LeftWall_High", shell, new Vector3(x, 2.425f, -0.07f), new Vector3(0.14f, 0.55f, 4.26f), wall, 0f);
            kit.Box("LeftWall_Front", shell, new Vector3(x, 1.55f, -1.325f), new Vector3(0.14f, 1.2f, 1.75f), wall, 0f);
            kit.Box("LeftWall_Back", shell, new Vector3(x, 1.55f, 1.455f), new Vector3(0.14f, 1.2f, 1.21f), wall, 0f);

            // Caps and skirting.
            kit.Box("BackCap", shell, new Vector3(-0.07f, WallHeight + 0.02f, BackWall + 0.07f), new Vector3(5.56f, 0.05f, 0.2f), trim, 0.02f);
            kit.Box("LeftCap", shell, new Vector3(x, WallHeight + 0.02f, -0.07f), new Vector3(0.2f, 0.05f, 4.42f), trim, 0.02f);
            kit.Box("BackEdge", shell, new Vector3(2.7f, WallHeight * 0.5f, BackWall + 0.07f), new Vector3(0.05f, WallHeight, 0.18f), trim, 0.02f);
            kit.Box("LeftEdge", shell, new Vector3(x, WallHeight * 0.5f, -2.2f), new Vector3(0.18f, WallHeight, 0.05f), trim, 0.02f);
            kit.Box("BackSkirting", shell, new Vector3(0f, 0.05f, BackWall - 0.012f), new Vector3(5.3f, 0.1f, 0.025f), trim, 0.008f);
            kit.Box("LeftSkirting", shell, new Vector3(LeftWall + 0.012f, 0.05f, -0.07f), new Vector3(0.025f, 0.1f, 4.2f), trim, 0.008f);
        }

        // ------------------------------------------------------------------ decor (not clickable)

        static void BuildDecor(Transform room)
        {
            var decor = Group("Decor", room);

            // Rug.
            kit.Disc("RugBorder", decor, new Vector3(0.35f, 0.007f, -0.35f), 2.3f, 0.014f, rugPink);
            kit.Disc("Rug", decor, new Vector3(0.35f, 0.009f, -0.35f), 2.08f, 0.018f, rugCream);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f + 0.3f;
                var p = new Vector3(0.35f + Mathf.Cos(a) * 0.82f, 0.019f, -0.35f + Mathf.Sin(a) * 0.82f);
                kit.Disc("RugFlower" + i, decor, p, 0.11f, 0.004f, rugPink);
            }

            // Bed with bunny-ear headboard.
            var bed = Group("Bed", decor);
            float bx = -0.95f;
            kit.Box("Base", bed, new Vector3(bx, 0.17f, 1.1f), new Vector3(1.26f, 0.26f, 1.9f), cream, 0.06f);
            kit.Box("Mattress", bed, new Vector3(bx, 0.37f, 1.12f), new Vector3(1.16f, 0.16f, 1.82f), white, 0.07f);
            kit.Box("Blanket", bed, new Vector3(bx, 0.47f, 0.86f), new Vector3(1.22f, 0.06f, 1.36f), pink, 0.03f);
            kit.Box("BlanketFold", bed, new Vector3(bx, 0.5f, 1.5f), new Vector3(1.22f, 0.07f, 0.16f), rugCream, 0.03f);
            kit.Box("Pillow", bed, new Vector3(bx, 0.52f, 1.8f), new Vector3(0.72f, 0.13f, 0.32f), white, 0.06f);
            kit.Box("Headboard", bed, new Vector3(bx, 0.68f, 2.0f), new Vector3(1.32f, 0.96f, 0.1f), pink, 0.06f);
            kit.Box("Footboard", bed, new Vector3(bx, 0.33f, 0.16f), new Vector3(1.28f, 0.46f, 0.08f), cream, 0.04f);
            foreach (float side in new[] { -0.28f, 0.28f })
            {
                kit.Sphere("Ear", bed, new Vector3(bx + side, 1.3f, 2.0f), new Vector3(0.24f, 0.46f, 0.08f), pink, new Vector3(0f, 0f, -side * 40f));
                kit.Sphere("EarInner", bed, new Vector3(bx + side * 1.01f, 1.31f, 1.958f), new Vector3(0.12f, 0.32f, 0.02f), pinkDeep, new Vector3(0f, 0f, -side * 40f));
            }
            Bunny(bed, new Vector3(bx + 0.28f, 0.53f, 1.55f), 1f);

            // Nightstand.
            var stand = Group("Nightstand", decor);
            kit.Box("Body", stand, new Vector3(0.05f, 0.27f, 1.84f), new Vector3(0.44f, 0.54f, 0.4f), cream, 0.04f);
            kit.Box("Drawer", stand, new Vector3(0.05f, 0.31f, 1.635f), new Vector3(0.34f, 0.16f, 0.02f), pink, 0.008f);
            kit.Sphere("Knob", stand, new Vector3(0.05f, 0.31f, 1.62f), Vector3.one * 0.035f, gold);
            Plant(stand, new Vector3(0.12f, 0.54f, 1.86f), 0.5f);

            // Writing desk under the window (the tarot sits on it) and its bunny chair.
            var desk = Group("WritingDesk", decor);
            kit.Box("Top", desk, new Vector3(-2.25f, 0.72f, 0.2f), new Vector3(0.62f, 0.05f, 1.26f), cream, 0.02f);
            kit.Box("Drawer", desk, new Vector3(-2.27f, 0.62f, 0.2f), new Vector3(0.54f, 0.12f, 0.8f), pink, 0.02f);
            kit.Sphere("Knob", desk, new Vector3(-1.99f, 0.62f, 0.2f), Vector3.one * 0.035f, gold);
            foreach (float lx in new[] { -2.5f, -2.0f })
                foreach (float lz in new[] { -0.38f, 0.78f })
                    kit.Cylinder("Leg", desk, new Vector3(lx, 0.35f, lz), 0.05f, 0.7f, wood);
            var chair = Group("BunnyChair", decor);
            kit.Box("Seat", chair, new Vector3(-1.62f, 0.45f, 0.2f), new Vector3(0.42f, 0.07f, 0.42f), pink, 0.03f);
            kit.Box("Back", chair, new Vector3(-1.4f, 0.74f, 0.2f), new Vector3(0.05f, 0.52f, 0.4f), pink, 0.025f);
            foreach (float ez in new[] { 0.1f, 0.3f })
                kit.Capsule("Ear", chair, new Vector3(-1.4f, 1.1f, ez), 0.1f, 0.3f, pink);
            kit.Sphere("Tail", chair, new Vector3(-1.37f, 0.62f, 0.2f), Vector3.one * 0.1f, white);
            foreach (float lx in new[] { -1.8f, -1.44f })
                foreach (float lz in new[] { 0.03f, 0.37f })
                    kit.Cylinder("Leg", chair, new Vector3(lx, 0.21f, lz), 0.04f, 0.42f, wood);

            // Computer desk along the back wall, with a shelf above it.
            var pcDesk = Group("ComputerDesk", decor);
            kit.Box("Top", pcDesk, new Vector3(1.57f, 0.725f, 1.74f), new Vector3(1.7f, 0.05f, 0.62f), cream, 0.02f);
            kit.Box("SideL", pcDesk, new Vector3(0.76f, 0.35f, 1.74f), new Vector3(0.06f, 0.7f, 0.58f), pink, 0.02f);
            kit.Box("Drawers", pcDesk, new Vector3(2.12f, 0.36f, 1.74f), new Vector3(0.52f, 0.68f, 0.58f), cream, 0.03f);
            kit.Box("DrawerA", pcDesk, new Vector3(2.12f, 0.52f, 1.445f), new Vector3(0.44f, 0.2f, 0.02f), pink, 0.008f);
            kit.Box("DrawerB", pcDesk, new Vector3(2.12f, 0.24f, 1.445f), new Vector3(0.44f, 0.2f, 0.02f), pink, 0.008f);
            kit.Sphere("KnobA", pcDesk, new Vector3(2.12f, 0.52f, 1.43f), Vector3.one * 0.035f, gold);
            kit.Sphere("KnobB", pcDesk, new Vector3(2.12f, 0.24f, 1.43f), Vector3.one * 0.035f, gold);
            kit.Box("Shelf", pcDesk, new Vector3(1.75f, 1.78f, 1.95f), new Vector3(1.1f, 0.04f, 0.22f), cream, 0.015f);
            var bookColors = new[] { lavender, pink, mint, pinkDeep, cream };
            for (int i = 0; i < 6; i++)
            {
                float h = 0.18f + (i * 37 % 5) * 0.012f;
                kit.Box("Book" + i, pcDesk, new Vector3(1.32f + i * 0.055f, 1.8f + h * 0.5f, 1.95f), new Vector3(0.045f, h, 0.16f), bookColors[i % bookColors.Length], 0.006f);
            }
            Plant(pcDesk, new Vector3(2.12f, 1.8f, 1.95f), 0.45f);
            Bunny(pcDesk, new Vector3(1.8f, 1.8f, 1.95f), 0.7f);

            // Gaming chair, turned a little towards the room.
            var gaming = Group("GamingChair", decor);
            gaming.SetPositionAndRotation(new Vector3(0.98f, 0f, 0.92f), Quaternion.Euler(0f, 32f, 0f));
            kit.Cylinder("Base", gaming, new Vector3(0f, 0.05f, 0f), 0.56f, 0.04f, pinkDeep);
            kit.Cylinder("Post", gaming, new Vector3(0f, 0.25f, 0f), 0.06f, 0.36f, cream);
            kit.Box("Seat", gaming, new Vector3(0f, 0.48f, 0f), new Vector3(0.52f, 0.1f, 0.5f), pink, 0.045f);
            kit.Box("Back", gaming, new Vector3(0f, 0.8f, -0.24f), new Vector3(0.5f, 0.56f, 0.1f), pink, 0.05f);
            kit.Box("Heart", gaming, new Vector3(0f, 0.8f, -0.3f), new Vector3(0.16f, 0.16f, 0.03f), white, 0.04f, new Vector3(0f, 0f, 45f));
            foreach (float side in new[] { -0.13f, 0.13f })
            {
                kit.Sphere("Ear", gaming, new Vector3(side, 1.16f, -0.24f), new Vector3(0.1f, 0.24f, 0.05f), pink, new Vector3(0f, 0f, -side * 120f));
                kit.Box("Arm", gaming, new Vector3(side * 2f, 0.64f, 0.02f), new Vector3(0.06f, 0.05f, 0.34f), cream, 0.02f);
            }

            // Plants and a pouf.
            Plant(decor, new Vector3(2.38f, 0f, 0.82f), 1.25f);
            Plant(decor, new Vector3(-1.75f, 0f, -1.95f), 0.95f);
            kit.Sphere("Pouf", decor, new Vector3(1.95f, 0.15f, -0.05f), new Vector3(0.56f, 0.3f, 0.56f), pink);
            kit.Sphere("PoufTop", decor, new Vector3(1.95f, 0.27f, -0.05f), new Vector3(0.42f, 0.09f, 0.42f), rugCream);

            // A small decorative frame next to the photo.
            var frame = Group("SmallFrame", decor);
            kit.Box("Frame", frame, new Vector3(-1.95f, 1.86f, BackWall - 0.015f), new Vector3(0.26f, 0.32f, 0.03f), wood, 0.01f);
            kit.Box("Card", frame, new Vector3(-1.95f, 1.86f, BackWall - 0.032f), new Vector3(0.2f, 0.26f, 0.006f), rugCream, 0.002f);
            kit.Sphere("Tulip", frame, new Vector3(-1.95f, 1.9f, BackWall - 0.04f), new Vector3(0.07f, 0.09f, 0.02f), pinkDeep);
            kit.Box("Stem", frame, new Vector3(-1.95f, 1.8f, BackWall - 0.037f), new Vector3(0.01f, 0.1f, 0.006f), greenDark, 0.002f);
        }

        static void Plant(Transform parent, Vector3 floorPoint, float scale)
        {
            var plant = Group("Plant", parent);
            plant.position = floorPoint;
            plant.localScale = Vector3.one * scale;
            kit.Cylinder("Pot", plant, new Vector3(0f, 0.14f, 0f), 0.26f, 0.28f, pot);
            kit.Cylinder("Soil", plant, new Vector3(0f, 0.28f, 0f), 0.22f, 0.01f, wood);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f;
                var leaf = kit.Sphere("Leaf" + i, plant, Quaternion.Euler(0f, a, 0f) * new Vector3(0.09f, 0.42f + (i % 2) * 0.08f, 0f),
                    new Vector3(0.12f, 0.26f, 0.05f), i % 2 == 0 ? green : greenDark, new Vector3(0f, a, -35f));
                leaf.name = "Leaf" + i;
            }
        }

        static void Bunny(Transform parent, Vector3 seat, float scale)
        {
            var bunny = Group("BunnyPlush", parent);
            bunny.position = seat;
            bunny.localScale = Vector3.one * scale;
            kit.Sphere("Body", bunny, new Vector3(0f, 0.07f, 0f), new Vector3(0.17f, 0.15f, 0.15f), white);
            kit.Sphere("Head", bunny, new Vector3(0f, 0.19f, -0.01f), new Vector3(0.15f, 0.13f, 0.13f), white);
            kit.Capsule("EarL", bunny, new Vector3(-0.035f, 0.3f, 0f), 0.045f, 0.13f, white, new Vector3(0f, 0f, 10f));
            kit.Capsule("EarR", bunny, new Vector3(0.035f, 0.3f, 0f), 0.045f, 0.13f, white, new Vector3(0f, 0f, -10f));
            kit.Sphere("Cheek", bunny, new Vector3(0.04f, 0.17f, -0.07f), Vector3.one * 0.025f, rugPink);
        }

        // ------------------------------------------------------------------ items

        static T NewItem<T>(string name, Transform parent, Vector3 position, string displayName, float focus, Vector3 focusOffset, out Transform model) where T : RoomItem
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            model = new GameObject(RoomItem.ModelChild).transform;
            model.SetParent(go.transform, false);
            var focusPoint = new GameObject("FocusPoint").transform;
            focusPoint.SetParent(go.transform, false);
            focusPoint.localPosition = focusOffset;
            var item = go.AddComponent<T>();
            Set(item, "displayName", displayName);
            Set(item, "focusPoint", focusPoint);
            Set(item, "cameraFocus", focus);
            return item;
        }

        static Transform Placeholder(Transform model) => Group("Placeholder", model);

        static WindowItem BuildWindow(Transform items, out Renderer sky)
        {
            var item = NewItem<WindowItem>("Item_Window", items, new Vector3(LeftWall, 0f, 0.2f), "窗戶", 0.25f, new Vector3(0.1f, 1.5f, 0f), out var model);
            var p = Placeholder(model);
            kit.Box("Sill", p, new Vector3(0.07f, 0.93f, 0f), new Vector3(0.2f, 0.05f, 1.52f), trim, 0.02f);
            kit.Box("FrameTop", p, new Vector3(0.0f, 2.17f, 0f), new Vector3(0.1f, 0.06f, 1.44f), trim, 0.015f);
            kit.Box("FrameLeft", p, new Vector3(0.0f, 1.55f, -0.69f), new Vector3(0.1f, 1.3f, 0.07f), trim, 0.015f);
            kit.Box("FrameRight", p, new Vector3(0.0f, 1.55f, 0.69f), new Vector3(0.1f, 1.3f, 0.07f), trim, 0.015f);
            kit.Box("Mullion", p, new Vector3(-0.05f, 1.55f, 0f), new Vector3(0.04f, 1.2f, 0.04f), trim, 0.01f);
            kit.Box("Transom", p, new Vector3(-0.05f, 1.66f, 0f), new Vector3(0.04f, 0.04f, 1.3f), trim, 0.01f);
            kit.Cylinder("CurtainRod", p, new Vector3(0.17f, 2.32f, 0f), 0.035f, 2.0f, gold, new Vector3(90f, 0f, 0f));
            foreach (float side in new[] { -1f, 1f })
            {
                kit.Sphere("RodEnd", p, new Vector3(0.17f, 2.32f, side * 1.02f), Vector3.one * 0.07f, gold);
                kit.Box("Curtain", p, new Vector3(0.13f, 1.56f, side * 0.88f), new Vector3(0.06f, 1.5f, 0.34f), pink, 0.025f);
                kit.Box("CurtainTie", p, new Vector3(0.17f, 1.22f, side * 0.88f), new Vector3(0.03f, 0.05f, 0.3f), pinkDeep, 0.012f);
            }

            // The view outside: a sky card behind the opening (an effect, not part of the click area).
            var skyGo = kit.Quad("Sky", item.transform, new Vector3(-0.38f, 1.55f, 0f), new Vector2(2.0f, 1.6f),
                PlaceholderKit.ShaderMaterial(PlaceholderKit.MaterialFolder + "/M_WindowSky.mat", "CompanionRoom/WindowSky"), new Vector3(0f, -90f, 0f));
            skyGo.AddComponent<RoomFx>();
            sky = skyGo.GetComponent<Renderer>();
            Set(item, "commentCooldown", 0f);
            return item;
        }

        static void BuildTarot(Transform items)
        {
            var item = NewItem<TarotDeckItem>("Item_Tarot", items, new Vector3(-2.22f, 0.745f, 0.22f), "塔羅牌", 0.45f, new Vector3(0f, 0.05f, 0f), out var model);
            var p = Placeholder(model);
            kit.Box("Cloth", p, new Vector3(0f, 0.004f, 0f), new Vector3(0.48f, 0.008f, 0.72f), cloth, 0.003f);
            kit.Box("ClothTrim", p, new Vector3(0f, 0.003f, 0f), new Vector3(0.5f, 0.006f, 0.74f), gold, 0.003f);
            kit.Box("Deck", p, new Vector3(0.04f, 0.03f, -0.2f), new Vector3(0.09f, 0.045f, 0.14f), cardBack, 0.006f, new Vector3(0f, 12f, 0f));
            kit.Box("DeckBand", p, new Vector3(0.04f, 0.053f, -0.2f), new Vector3(0.092f, 0.002f, 0.03f), gold, 0.0005f, new Vector3(0f, 12f, 0f));
            for (int i = 0; i < 3; i++)
                kit.Box("Card" + i, p, new Vector3(0.06f, 0.011f, 0.0f + i * 0.12f), new Vector3(0.075f, 0.004f, 0.115f), i == 1 ? cardBack : cardFace, 0.0015f, new Vector3(0f, -8f + i * 8f, 0f));
            kit.Cylinder("BallStand", p, new Vector3(-0.13f, 0.016f, 0.16f), 0.09f, 0.03f, gold);
            kit.Sphere("CrystalBall", p, new Vector3(-0.13f, 0.09f, 0.16f), Vector3.one * 0.12f, lavender);
            kit.Cylinder("Candle", p, new Vector3(-0.12f, 0.06f, -0.15f), 0.05f, 0.11f, cream);
            kit.Sphere("Flame", p, new Vector3(-0.12f, 0.13f, -0.15f), new Vector3(0.018f, 0.035f, 0.018f), kit.Glowing("Flame", "#FFD9A0", "#FFA24F", 3f));
        }

        static void BuildPhoto(Transform items)
        {
            var item = NewItem<PhotoFrameItem>("Item_Photo", items, new Vector3(-0.95f, 2.1f, BackWall - 0.022f), "相片", 0.5f, Vector3.zero, out var model);
            var p = Placeholder(model);
            var frameWood = kit.Mat("FrameWood", "#C99C76", 0.3f);
            kit.Box("FrameTop", p, new Vector3(0f, 0.21f, 0f), new Vector3(0.64f, 0.05f, 0.045f), frameWood, 0.012f);
            kit.Box("FrameBottom", p, new Vector3(0f, -0.21f, 0f), new Vector3(0.64f, 0.05f, 0.045f), frameWood, 0.012f);
            kit.Box("FrameLeft", p, new Vector3(-0.295f, 0f, 0f), new Vector3(0.05f, 0.47f, 0.045f), frameWood, 0.012f);
            kit.Box("FrameRight", p, new Vector3(0.295f, 0f, 0f), new Vector3(0.05f, 0.47f, 0.045f), frameWood, 0.012f);
            kit.Box("Backing", p, new Vector3(0f, 0f, 0.01f), new Vector3(0.56f, 0.4f, 0.015f), rugCream, 0.003f);
            var photoMaterial = kit.Mat("PhotoSurface", "#FFFFFF", 0.45f);
            kit.Quad("Photo", p, new Vector3(0f, 0f, -0.003f), new Vector2(0.52f, 0.37f), photoMaterial);
            kit.Sphere("Bow", p, new Vector3(0f, 0.25f, -0.02f), new Vector3(0.09f, 0.05f, 0.03f), pinkDeep);
            Set(item, "surfaceAspect", 0.52f / 0.37f);
        }

        static void BuildLamp(Transform items)
        {
            var item = NewItem<WallLampItem>("Item_WallLamp", items, new Vector3(0.05f, 1.62f, BackWall), "壁燈", 0.2f, new Vector3(0f, 0.02f, -0.18f), out var model);
            var p = Placeholder(model);
            kit.Box("Plate", p, new Vector3(0f, 0f, -0.01f), new Vector3(0.12f, 0.18f, 0.02f), gold, 0.008f);
            kit.Box("Arm", p, new Vector3(0f, 0.02f, -0.08f), new Vector3(0.03f, 0.03f, 0.14f), gold, 0.01f);
            var shade = kit.Mat("LampShade", "#FFF3DE", 0.35f, 0f, null, null, true);
            var bulb = kit.Sphere("Bulb", p, new Vector3(0f, 0.02f, -0.19f), new Vector3(0.2f, 0.18f, 0.2f), shade);
            kit.Capsule("EarL", p, new Vector3(-0.05f, 0.15f, -0.19f), 0.06f, 0.16f, shade, new Vector3(0f, 0f, 12f));
            kit.Capsule("EarR", p, new Vector3(0.05f, 0.15f, -0.19f), 0.06f, 0.16f, shade, new Vector3(0f, 0f, -12f));

            var lightGo = new GameObject("LampLight");
            lightGo.transform.SetParent(item.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.02f, -0.34f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = PlaceholderKit.Hex("#FFC789");
            light.range = 4.2f;
            light.intensity = 0f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            // The lamp's own shade and ears sit inside this radius; they must not shadow the wall.
            light.shadowNearPlane = 0.35f;
            light.enabled = false;

            var halo = kit.Quad("Halo", item.transform, new Vector3(0f, 0.02f, -0.2f), new Vector2(0.7f, 0.7f),
                PlaceholderKit.ShaderMaterial(PlaceholderKit.MaterialFolder + "/M_LampHalo.mat", "CompanionRoom/SoftGlow"));
            halo.AddComponent<RoomFx>();
            var haloRenderer = halo.GetComponent<Renderer>();
            haloRenderer.enabled = false;

            Set(item, "lamp", light);
            Set(item, "glowParts", new Object[] { bulb.GetComponent<Renderer>() });
            Set(item, "halo", haloRenderer);
            Set(item, "commentCooldown", 25f);
            Set(item, "colliderPadding", new Vector3(0.07f, 0.07f, 0.07f));
        }

        static void BuildComputer(Transform items)
        {
            var item = NewItem<ComputerItem>("Item_Computer", items, new Vector3(1.4f, 0.75f, 1.84f), "電腦", 0.45f, new Vector3(0f, 0.42f, 0f), out var model);
            var p = Placeholder(model);
            kit.Box("StandBase", p, new Vector3(0f, 0.01f, 0.06f), new Vector3(0.26f, 0.02f, 0.16f), cream, 0.008f);
            kit.Box("StandNeck", p, new Vector3(0f, 0.15f, 0.08f), new Vector3(0.05f, 0.27f, 0.03f), cream, 0.012f);
            kit.Box("Monitor", p, new Vector3(0f, 0.43f, 0.045f), new Vector3(0.84f, 0.52f, 0.05f), cream, 0.025f);
            var screenMaterial = kit.Mat("Screen", "#2B2A36", 0.6f, 0f, null, null, true);
            kit.Quad("Screen", p, new Vector3(0f, 0.44f, 0.0175f), new Vector2(0.76f, 0.43f), screenMaterial);
            foreach (float side in new[] { -1f, 1f })
                kit.Sphere("Ear", p, new Vector3(side * 0.28f, 0.74f, 0.05f), new Vector3(0.09f, 0.2f, 0.03f), pink, new Vector3(0f, 0f, -side * 16f));
            kit.Sphere("BowL", p, new Vector3(-0.05f, 0.69f, 0.02f), new Vector3(0.08f, 0.06f, 0.04f), pinkDeep);
            kit.Sphere("BowR", p, new Vector3(0.05f, 0.69f, 0.02f), new Vector3(0.08f, 0.06f, 0.04f), pinkDeep);
            kit.Box("Keyboard", p, new Vector3(0f, 0.012f, -0.22f), new Vector3(0.48f, 0.022f, 0.15f), white, 0.008f);
            kit.Box("Keys", p, new Vector3(0f, 0.025f, -0.22f), new Vector3(0.42f, 0.004f, 0.1f), rugPink, 0.002f);
            kit.Sphere("Mouse", p, new Vector3(0.34f, 0.015f, -0.22f), new Vector3(0.06f, 0.03f, 0.09f), pink);
            kit.Cylinder("Mug", p, new Vector3(-0.42f, 0.05f, -0.12f), 0.08f, 0.1f, pink);

            var lightGo = new GameObject("ScreenLight");
            lightGo.transform.SetParent(item.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.44f, -0.3f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = PlaceholderKit.Hex("#DCCBFF");
            light.range = 1.6f;
            light.intensity = 0f;
            light.enabled = false;
            Set(item, "screenLight", light);
        }

        static void BuildCabinet(Transform items)
        {
            // Faces +x (into the room); its back is against the left wall.
            var item = NewItem<RecordCabinetItem>("Item_Cabinet", items, new Vector3(LeftWall + 0.26f, 0f, -1.42f), "占卜紀錄櫃", 0.4f, new Vector3(0.2f, 0.95f, 0f), out var model);
            var p = Placeholder(model);
            kit.Box("Lower", p, new Vector3(0f, 0.34f, 0f), new Vector3(0.5f, 0.6f, 0.9f), cream, 0.035f);
            kit.Box("DrawerA", p, new Vector3(0.255f, 0.48f, 0f), new Vector3(0.02f, 0.22f, 0.8f), pink, 0.008f);
            kit.Box("DrawerB", p, new Vector3(0.255f, 0.22f, 0f), new Vector3(0.02f, 0.22f, 0.8f), pink, 0.008f);
            kit.Sphere("KnobA", p, new Vector3(0.27f, 0.48f, 0f), Vector3.one * 0.04f, gold);
            kit.Sphere("KnobB", p, new Vector3(0.27f, 0.22f, 0f), Vector3.one * 0.04f, gold);
            foreach (float fz in new[] { -0.38f, 0.38f })
                foreach (float fx in new[] { -0.18f, 0.18f })
                    kit.Cylinder("Foot", p, new Vector3(fx, 0.02f, fz), 0.06f, 0.04f, wood);
            kit.Box("BackPanel", p, new Vector3(-0.23f, 0.96f, 0f), new Vector3(0.04f, 0.68f, 0.9f), inner, 0f);
            kit.Box("SideFront", p, new Vector3(0f, 0.96f, -0.43f), new Vector3(0.5f, 0.68f, 0.04f), cream, 0.012f);
            kit.Box("SideBack", p, new Vector3(0f, 0.96f, 0.43f), new Vector3(0.5f, 0.68f, 0.04f), cream, 0.012f);
            kit.Box("Shelf", p, new Vector3(0f, 0.96f, 0f), new Vector3(0.44f, 0.02f, 0.82f), cream, 0f);
            kit.Box("Top", p, new Vector3(0f, 1.32f, 0f), new Vector3(0.56f, 0.05f, 0.96f), pink, 0.02f);
            var books = new[] { lavender, pinkDeep, mint, cardBack, pink };
            for (int i = 0; i < 5; i++)
            {
                kit.Box("RecordBook" + i, p, new Vector3(-0.02f, 0.76f, -0.3f + i * 0.07f), new Vector3(0.3f, 0.26f, 0.05f), books[i], 0.008f);
                kit.Box("Scroll" + i, p, new Vector3(-0.02f, 1.03f, -0.28f + i * 0.13f), new Vector3(0.26f, 0.1f, 0.1f), i % 2 == 0 ? rugCream : cardFace, 0.04f);
            }

            // The door hinges on its far edge (z = +0.43) and swings out towards the room.
            var door = new GameObject("Door").transform;
            door.SetParent(p, false);
            door.localPosition = new Vector3(0.265f, 0.96f, 0.43f);
            kit.Box("Panel", door, new Vector3(0f, 0f, -0.43f), new Vector3(0.03f, 0.66f, 0.86f), pink, 0.02f);
            kit.Cylinder("Emblem", door, new Vector3(0.018f, 0.05f, -0.43f), 0.2f, 0.006f, gold, new Vector3(0f, 0f, 90f));
            kit.Cylinder("EmblemInner", door, new Vector3(0.022f, 0.05f, -0.43f), 0.16f, 0.006f, cardBack, new Vector3(0f, 0f, 90f));
            kit.Sphere("Knob", door, new Vector3(0.03f, -0.05f, -0.79f), Vector3.one * 0.045f, gold);

            Plant(p, new Vector3(0f, 1.345f, 0.24f), 0.55f);
            kit.Sphere("Crystal", p, new Vector3(0.05f, 1.42f, -0.22f), new Vector3(0.11f, 0.14f, 0.11f), lavender);
            Set(item, "door", door);
        }

        // ------------------------------------------------------------------ 凡安

        static RoomCompanion BuildCompanion(Transform cameraTransform)
        {
            var rootGo = new GameObject("凡安");
            rootGo.transform.position = new Vector3(0.45f, 0f, -0.42f);
            var model = new GameObject(RoomItem.ModelChild).transform;
            model.SetParent(rootGo.transform, false);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPrefab);
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, model);
                instance.transform.localPosition = Vector3.zero;
                var bounds = RendererBounds(instance);
                float height = Mathf.Max(0.1f, bounds.size.y);
                instance.transform.localScale *= 1.2f / height;
                bounds = RendererBounds(instance);
                instance.transform.localPosition += Vector3.up * (0f - bounds.min.y);
            }
            else
            {
                var p = Placeholder(model);
                kit.Capsule("Body", p, new Vector3(0f, 0.42f, 0f), 0.42f, 0.84f, lavender);
                kit.Sphere("Head", p, new Vector3(0f, 0.95f, 0f), Vector3.one * 0.5f, cream);
            }

            var companion = rootGo.AddComponent<RoomCompanion>();
            Set(companion, "restLookTarget", cameraTransform);
            var talk = rootGo.AddComponent<CompanionTalkItem>();
            Set(talk, "displayName", "凡安");
            Set(talk, "cameraFocus", 0.3f);
            var focus = new GameObject("FocusPoint").transform;
            focus.SetParent(rootGo.transform, false);
            focus.localPosition = new Vector3(0f, 0.8f, 0f);
            Set(talk, "focusPoint", focus);

            // Start facing the camera.
            var flat = cameraTransform.position - rootGo.transform.position;
            flat.y = 0f;
            rootGo.transform.rotation = Quaternion.LookRotation(flat);
            return companion;
        }

        static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                b.Encapsulate(r.bounds);
            return b;
        }

        // ------------------------------------------------------------------ camera, light, post

        static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(5.9f, 5.3f, -6.6f);
            var lookAt = new Vector3(-0.15f, 0.45f, 0.05f);
            go.transform.rotation = Quaternion.LookRotation(lookAt - go.transform.position);
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PlaceholderKit.Hex("#F6DDE0");
            camera.allowHDR = true;
            go.AddComponent<AudioListener>();
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            var rig = go.AddComponent<RoomCameraRig>();
            rig.Configure(lookAt);
            return camera;
        }

        static Volume BuildVolume()
        {
            AssetDatabase.DeleteAsset(VolumePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);
            T Add<T>() where T : VolumeComponent
            {
                var component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
                return component;
            }
            Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var bloom = Add<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.65f);
            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0f);
            color.contrast.Override(4f);
            color.saturation.Override(6f);
            color.colorFilter.Override(Color.white);
            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.5f);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("PostProcess");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            return volume;
        }

        static RoomLighting BuildLighting(Camera camera, Volume volume, Renderer sky)
        {
            var root = new GameObject("Lighting");
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root.transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowNormalBias = 0.3f;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.rotation = Quaternion.Euler(36f, -58f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.shadows = LightShadows.None;

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            var lighting = root.AddComponent<RoomLighting>();
            lighting.Configure(sun, fill, camera, volume, new[] { sky });
            lighting.Apply(lighting.Get(TimeOfDay.Morning));
            return lighting;
        }

        static void BuildApp(Camera camera, RoomCompanion companion, RoomLighting lighting)
        {
            var go = new GameObject("CompanionRoomApp");
            var audio = go.AddComponent<RoomAudio>();
            var app = go.AddComponent<CompanionRoomApp>();
            app.Configure(EnsureSkin(), companion, lighting, camera.GetComponent<RoomCameraRig>(), audio);
            go.AddComponent<RoomInteractor>().Configure(camera);

            // The module gets its default UI actions at runtime (RoomUI.EnsureEventSystem).
            var events = new GameObject("EventSystem", typeof(EventSystem));
            events.AddComponent<InputSystemUIInputModule>();

            EnsureOutlineMaterial();
        }

        static RoomUISkin EnsureSkin()
        {
            var skin = AssetDatabase.LoadAssetAtPath<RoomUISkin>(SkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<RoomUISkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }
            return skin;
        }

        static void EnsureOutlineMaterial()
        {
            var m = PlaceholderKit.ShaderMaterial(OutlineMaterialPath, "CompanionRoom/HoverOutline");
            m.SetColor("_OutlineColor", new Color(1.7f, 1.3f, 0.8f, 1f));
            m.SetFloat("_OutlineWidth", 0f);
        }

        // ------------------------------------------------------------------ project setup

        static void EnsureCompanionLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == RoomCompanion.LayerName)
                    return;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = RoomCompanion.LayerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
            Debug.LogWarning("[CompanionRoom] No free layer for \"Companion\"; the dialogue portrait will show the whole room.");
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ helpers

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>Sets a serialized field by name (private [SerializeField]s included).</summary>
        static void Set(Object target, string property, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property) ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{property}'.");
            switch (value)
            {
                case string s: p.stringValue = s; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case Color c: p.colorValue = c; break;
                case Vector3 v: p.vector3Value = v; break;
                case Object[] array:
                    p.arraySize = array.Length;
                    for (int i = 0; i < array.Length; i++)
                        p.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                    break;
                case Object o: p.objectReferenceValue = o; break;
                case null: p.objectReferenceValue = null; break;
                default: throw new ArgumentException($"Unsupported value type {value.GetType().Name}.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
