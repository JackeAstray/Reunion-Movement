using ReunionMovement.Core.UIInput;
using ReunionMovement.UI.Select;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReunionMovement.EditorTools
{
    /// <summary>
    /// 一键生成「开始菜单 + 本地多人选择」示例：
    /// 开始页面 → 首页 → 开始游戏/历史决策简报/设置/退出；
    /// 设置 → 音量/分辨率/多语言切换；
    /// 开始游戏 → 本地多人单机游戏。
    /// </summary>
    public static class CreateLocalMultiplayerSelectionExample
    {
        private static readonly string ScenePath =
            ReunionMovement.Editor.Shared.ReunionPaths.InWritable("Editor/UIScenes/StartMenuExampleScene.unity");

        private static Font cachedFont;

        private static readonly Color[] SwatchColors =
        {
            new Color(0.90f, 0.16f, 0.16f, 1f), // 红
            new Color(0.15f, 0.42f, 0.95f, 1f), // 蓝
            new Color(0.18f, 0.78f, 0.30f, 1f), // 绿
            new Color(0.95f, 0.83f, 0.15f, 1f), // 黄
            new Color(0.62f, 0.28f, 0.90f, 1f), // 紫
            new Color(0.95f, 0.50f, 0.13f, 1f), // 橙
            new Color(0.15f, 0.82f, 0.85f, 1f), // 青
            new Color(0.92f, 0.92f, 0.92f, 1f), // 白
        };

        private sealed class MenuLevel
        {
            public GameObject panel;
            public CarouselSelectionController carousel;
            public bool advanceOnConfirm;
            public int[] nextLevelByOption;
        }

        [MenuItem("ReunionMovement/内容/生成示例场景", false, 54)]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEventSystem();

            var canvas = CreateCanvas();
            CreateImage("Background", canvas.transform, new Color(0.04f, 0.04f, 0.05f, 1f), false);

            var root = new GameObject("MenuRoot", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Stretch(root.GetComponent<RectTransform>());

            var stack = root.AddComponent<CarouselMenuStack>();
            var actions = root.AddComponent<StartMenuActions>();

            var startPage = CreateMenuPanel(root.transform, "StartPagePanel", "开始页面",
                "按确认进入首页", new[] { "开始" });
            var home = CreateMenuPanel(root.transform, "HomePanel", "首页",
                "选择菜单项", new[] { "开始游戏", "历史决策简报", "设置", "退出" }, vertical: true);
            var startGame = CreateMenuPanel(root.transform, "StartGamePanel", "开始游戏",
                "选择游戏模式", new[] { "本地多人单机游戏", "返回" }, vertical: true);
            var history = CreateMenuPanel(root.transform, "HistoryPanel", "历史决策简报",
                "历史决策简报（示例占位）", new[] { "返回" }, vertical: true);
            var settingsPanel = CreateSettingsPanel(root.transform, stack);
            var localPanel = CreateLocalPanel(root.transform);

            var levels = new[]
            {
                new MenuLevel { panel = startPage.panel, carousel = startPage.carousel, advanceOnConfirm = true, nextLevelByOption = new[] { 1 } },
                new MenuLevel { panel = home.panel, carousel = home.carousel, advanceOnConfirm = true, nextLevelByOption = new[] { 2, 3, 4, -1 } },
                new MenuLevel { panel = startGame.panel, carousel = startGame.carousel, advanceOnConfirm = true, nextLevelByOption = new[] { 5, 1 } },
                new MenuLevel { panel = history.panel, carousel = history.carousel, advanceOnConfirm = true, nextLevelByOption = new[] { 1 } },
                new MenuLevel { panel = settingsPanel, carousel = null, advanceOnConfirm = false, nextLevelByOption = null },
                new MenuLevel { panel = localPanel, carousel = null, advanceOnConfirm = false, nextLevelByOption = null },
            };

            ConfigureStack(stack, levels);
            ConfigureActions(actions, stack);

            // 编辑器里先显示开始页，其它面板由菜单栈按需激活
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i].panel != null) levels[i].panel.SetActive(i == 0);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = root;
            Debug.Log($"[CreateStartMenuExample] 已生成示例场景：{ScenePath}");
        }

        private static (GameObject panel, CarouselSelectionController carousel) CreateMenuPanel(
            Transform parent,
            string name,
            string title,
            string hint,
            string[] options,
            bool vertical = false)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());

            var background = panel.GetComponent<Image>();
            background.color = new Color(0.07f, 0.07f, 0.09f, 1f);
            background.raycastTarget = false;

            panel.AddComponent<UIFocusScope>();

            CreateText("Title", panel.transform, title, 46, new Vector2(0f, 360f), new Vector2(1000f, 70f));
            CreateText("Hint", panel.transform, hint, 22, new Vector2(0f, -300f), new Vector2(1500f, 40f));

            var carouselGo = new GameObject("MenuCarousel", typeof(RectTransform));
            carouselGo.transform.SetParent(panel.transform, false);
            var rect = carouselGo.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = vertical ? new Vector2(520f, 520f) : new Vector2(1700f, 120f);
            rect.anchoredPosition = vertical ? Vector2.zero : new Vector2(0f, 40f);

            if (vertical)
            {
                var layout = carouselGo.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 12f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }
            else
            {
                var layout = carouselGo.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 20f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            var carousel = carouselGo.AddComponent<CarouselSelectionController>();
            var buttonSize = vertical ? new Vector2(420f, 68f) : new Vector2(260f, 80f);
            var buttons = new Button[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                buttons[i] = CreateButton(
                    $"Option_{i}",
                    options[i],
                    carouselGo.transform,
                    Vector2.zero,
                    buttonSize,
                    new Color(0.18f, 0.18f, 0.18f, 1f));
            }

            SetCarouselOptions(carousel, buttons, vertical);
            return (panel, carousel);
        }

        private static GameObject CreateSettingsPanel(Transform parent, CarouselMenuStack stack)
        {
            var panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());

            var background = panel.GetComponent<Image>();
            background.color = new Color(0.07f, 0.07f, 0.09f, 1f);
            background.raycastTarget = false;
            panel.AddComponent<UIFocusScope>();

            CreateText("Title", panel.transform, "设置", 46, new Vector2(0f, 360f), new Vector2(1000f, 70f));
            CreateText("Hint", panel.transform, "上下选择 / 左右调整 / 确认切换全屏 / 取消返回", 20,
                new Vector2(0f, -300f), new Vector2(1400f, 40f));

            var volumeRow = CreateSettingsRow(panel.transform, "VolumeRow", "主音量", 180f);
            var volumeSlider = CreateSlider(volumeRow.transform, "VolumeSlider",
                new Vector2(90f, 0f), new Vector2(360f, 28f));
            var volumeValue = CreateText("Value", volumeRow.transform, "100%", 22,
                new Vector2(350f, 0f), new Vector2(100f, 40f));

            var resolutionRow = CreateSettingsRow(panel.transform, "ResolutionRow", "分辨率", 60f);
            var resolutionDropdown = CreateDropdown(resolutionRow.transform, "ResolutionDropdown",
                new Vector2(140f, 0f), new Vector2(360f, 44f),
                new[] { "1280 x 720", "1600 x 900", "1920 x 1080" });

            var fullscreenRow = CreateSettingsRow(panel.transform, "FullscreenRow", "全屏", -60f);
            var fullscreenToggle = CreateToggle(fullscreenRow.transform, "FullscreenToggle",
                new Vector2(320f, 0f));

            var languageRow = CreateSettingsRow(panel.transform, "LanguageRow", "语言", -180f);
            var languageDropdown = CreateDropdown(languageRow.transform, "LanguageDropdown",
                new Vector2(140f, 0f), new Vector2(360f, 44f),
                new[] { "中文", "English", "Русский", "日本語" });

            var controller = panel.AddComponent<SettingsPanelController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("stack").objectReferenceValue = stack;
            serialized.FindProperty("homeLevel").intValue = 1;
            serialized.FindProperty("volumeSlider").objectReferenceValue = volumeSlider;
            serialized.FindProperty("volumeValueLabel").objectReferenceValue = volumeValue;
            serialized.FindProperty("resolutionDropdown").objectReferenceValue = resolutionDropdown;
            serialized.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenToggle;
            serialized.FindProperty("languageDropdown").objectReferenceValue = languageDropdown;

            var rows = serialized.FindProperty("rowBackgrounds");
            rows.arraySize = 4;
            rows.GetArrayElementAtIndex(0).objectReferenceValue = volumeRow;
            rows.GetArrayElementAtIndex(1).objectReferenceValue = resolutionRow;
            rows.GetArrayElementAtIndex(2).objectReferenceValue = fullscreenRow;
            rows.GetArrayElementAtIndex(3).objectReferenceValue = languageRow;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            var backButton = CreateButton("BackButton", "返回", panel.transform,
                new Vector2(760f, -400f), new Vector2(160f, 48f),
                new Color(0.18f, 0.18f, 0.18f, 1f));
            var backSerialized = new SerializedObject(controller);
            backSerialized.FindProperty("backButton").objectReferenceValue = backButton;
            backSerialized.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static Image CreateSettingsRow(Transform parent, string name, string label, float y)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);

            var rect = row.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(1000f, 76f);

            var image = row.GetComponent<Image>();
            image.color = new Color(0.11f, 0.11f, 0.13f, 1f);
            image.raycastTarget = false;

            var labelText = CreateText("Label", row.transform, label, 26,
                new Vector2(-300f, 0f), new Vector2(320f, 48f));
            labelText.alignment = TextAnchor.MiddleLeft;

            return image;
        }

        private static Slider CreateSlider(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var track = CreateImage("Track", go.transform, new Color(0.22f, 0.22f, 0.25f, 1f), true);
            var fill = CreateImage("Fill", go.transform, new Color(0.25f, 0.55f, 0.95f, 1f), false);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var handle = CreateImage("Handle", go.transform, Color.white, true);
            var handleRect = handle.rectTransform;
            handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(20f, 36f);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.transition = Selectable.Transition.None;
            return slider;
        }

        private static Dropdown CreateDropdown(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            string[] options)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var background = go.GetComponent<Image>();
            background.color = new Color(0.18f, 0.18f, 0.20f, 1f);
            background.raycastTarget = true;

            var dropdown = go.GetComponent<Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.transition = Selectable.Transition.None;

            var caption = CreateText("Label", go.transform, options[0], 22, Vector2.zero, size);
            caption.alignment = TextAnchor.MiddleLeft;
            var captionRect = caption.rectTransform;
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(12f, 0f);
            captionRect.offsetMax = new Vector2(-36f, 0f);
            dropdown.captionText = caption;

            CreateText("Arrow", go.transform, "▼", 18,
                new Vector2(size.x * 0.5f - 20f, 0f), new Vector2(24f, 24f));

            var template = CreateDropdownTemplate(go.transform, size);
            dropdown.template = template.GetComponent<RectTransform>();
            dropdown.itemText = template.GetComponentInChildren<Text>(true);

            dropdown.options.Clear();
            for (int i = 0; i < options.Length; i++)
            {
                dropdown.options.Add(new Dropdown.OptionData(options[i]));
            }
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static GameObject CreateDropdownTemplate(Transform parent, Vector2 size)
        {
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(parent, false);

            var rect = template.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            rect.sizeDelta = new Vector2(0f, 180f);

            var image = template.GetComponent<Image>();
            image.color = new Color(0.10f, 0.10f, 0.12f, 1f);
            image.raycastTarget = true;

            var scroll = template.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(template.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var item = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 36f);

            var itemImage = item.GetComponent<Image>();
            itemImage.color = new Color(0.16f, 0.16f, 0.18f, 1f);
            itemImage.raycastTarget = true;

            var itemToggle = item.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemImage;

            var itemLabel = CreateText("Item Label", item.transform, "Option", 20,
                Vector2.zero, new Vector2(0f, 36f));
            itemLabel.alignment = TextAnchor.MiddleLeft;
            var itemLabelRect = itemLabel.rectTransform;
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(12f, 0f);
            itemLabelRect.offsetMax = new Vector2(-12f, 0f);

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            template.SetActive(false);
            return template;
        }

        private static Toggle CreateToggle(Transform parent, string name, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(44f, 44f);

            var background = go.GetComponent<Image>();
            background.color = new Color(0.18f, 0.18f, 0.20f, 1f);
            background.raycastTarget = true;

            var checkmark = CreateImage("Checkmark", go.transform,
                new Color(0.25f, 0.55f, 0.95f, 1f), false);
            var checkRect = checkmark.rectTransform;
            checkRect.anchorMin = checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = Vector2.zero;
            checkRect.sizeDelta = new Vector2(26f, 26f);

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            return toggle;
        }

        private static GameObject CreateLocalPanel(Transform parent)
        {
            var panel = new GameObject("LocalMultiplayerPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());

            var background = panel.GetComponent<Image>();
            background.color = new Color(0.04f, 0.04f, 0.05f, 1f);
            background.raycastTarget = false;
            panel.AddComponent<UIFocusScope>();

            CreateText("Title", panel.transform, "本地多人单机游戏", 46, new Vector2(0f, 470f), new Vector2(1000f, 64f));
            var stageLabel = CreateText("StageLabel", panel.transform, "选择颜色 → 位置 → 准备", 34,
                new Vector2(0f, 408f), new Vector2(1000f, 48f));
            var countdownLabel = CreateText("CountdownLabel", panel.transform, string.Empty, 84,
                new Vector2(0f, 330f), new Vector2(400f, 100f));
            countdownLabel.color = new Color(1f, 0.82f, 0.2f, 1f);

            var hintLabel = CreateText("HintLabel", panel.transform,
                "选颜色后自动进入位置；所有玩家准备后自动开始",
                22, new Vector2(0f, 276f), new Vector2(1400f, 36f));

            var manager = panel.AddComponent<LocalMultiplayerSelectionManager>();
            CreateTrack(panel.transform, manager);

            var cards = new LocalMultiplayerPlayerCard[4];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = CreatePlayerCard(panel.transform, i);
            }

            ConfigureManager(manager, cards, stageLabel, countdownLabel, hintLabel);

            for (int i = 1; i < cards.Length; i++)
            {
                cards[i].gameObject.SetActive(false);
            }

            return panel;
        }

        private static void ConfigureStack(CarouselMenuStack stack, MenuLevel[] levels)
        {
            var serialized = new SerializedObject(stack);
            var levelsProperty = serialized.FindProperty("levels");
            levelsProperty.arraySize = levels.Length;

            for (int i = 0; i < levels.Length; i++)
            {
                var levelProperty = levelsProperty.GetArrayElementAtIndex(i);
                levelProperty.FindPropertyRelative("panel").objectReferenceValue = levels[i].panel;
                levelProperty.FindPropertyRelative("carousel").objectReferenceValue = levels[i].carousel;
                levelProperty.FindPropertyRelative("advanceOnConfirm").boolValue = levels[i].advanceOnConfirm;

                var next = levelProperty.FindPropertyRelative("nextLevelByOption");
                if (levels[i].nextLevelByOption == null || levels[i].nextLevelByOption.Length == 0)
                {
                    next.arraySize = 0;
                }
                else
                {
                    next.arraySize = levels[i].nextLevelByOption.Length;
                    for (int j = 0; j < levels[i].nextLevelByOption.Length; j++)
                    {
                        next.GetArrayElementAtIndex(j).intValue = levels[i].nextLevelByOption[j];
                    }
                }
            }

            serialized.FindProperty("openFirstLevelOnEnable").boolValue = true;
            serialized.FindProperty("closeRootOnCancel").boolValue = false;
            serialized.FindProperty("closeOnCompleted").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureActions(StartMenuActions actions, CarouselMenuStack stack)
        {
            var serialized = new SerializedObject(actions);
            serialized.FindProperty("stack").objectReferenceValue = stack;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetCarouselOptions(
            CarouselSelectionController carousel,
            Button[] buttons,
            bool vertical)
        {
            var serialized = new SerializedObject(carousel);
            var options = serialized.FindProperty("options");
            options.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                options.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            }

            serialized.FindProperty("columns").intValue = 1;
            serialized.FindProperty("navigateAxis").enumValueIndex = vertical ? 1 : 0;
            serialized.FindProperty("playerSlot").intValue = 0;
            serialized.FindProperty("closeOnConfirm").boolValue = false;
            serialized.FindProperty("closeOnCancel").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateCanvas()
        {
            var go = new GameObject(
                "StartMenuCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return go;
        }

        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = go.GetComponent<InputSystemUIInputModule>();

            var actions = Resources.Load<InputActionAsset>("InputSystem_Actions");
            if (actions != null)
            {
                module.actionsAsset = Object.Instantiate(actions);
                module.enabled = true;
            }
            else
            {
                Debug.LogWarning("[CreateStartMenuExample] 未找到 InputSystem_Actions，请手动赋 actionsAsset。");
            }
        }

        private static void CreateTrack(Transform parent, LocalMultiplayerSelectionManager manager)
        {
            var track = CreateImage("Track", parent, new Color(0.09f, 0.1f, 0.12f, 1f), false);
            var rect = track.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 120f);
            rect.sizeDelta = new Vector2(1400f, 180f);

            CreateText("TrackLabel", track.transform, "赛道 / 位置", 22, new Vector2(0f, 64f), new Vector2(400f, 32f));

            var view = track.gameObject.AddComponent<LocalMultiplayerTrackView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("manager").objectReferenceValue = manager;

            var slotsProperty = serializedView.FindProperty("slots");
            slotsProperty.arraySize = 4;

            float[] xs = { -480f, -160f, 160f, 480f };
            for (int i = 0; i < xs.Length; i++)
            {
                var slot = CreateImage($"PositionSlot_{i + 1}", track.transform, new Color(0.16f, 0.18f, 0.22f, 1f), false);
                var slotRect = slot.rectTransform;
                slotRect.anchorMin = slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotRect.anchoredPosition = new Vector2(xs[i], -10f);
                slotRect.sizeDelta = new Vector2(240f, 88f);

                CreateText($"Label_{i + 1}", slot.transform, $"位置 {i + 1}", 20, new Vector2(0f, 28f), new Vector2(220f, 24f));

                var markers = new Image[4];
                var labels = new Text[4];
                for (int m = 0; m < markers.Length; m++)
                {
                    var marker = CreateImage($"Marker_{m + 1}", slot.transform, Color.white, false);
                    var markerRect = marker.rectTransform;
                    markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
                    markerRect.anchoredPosition = new Vector2(-78f + m * 52f, -20f);
                    markerRect.sizeDelta = new Vector2(40f, 40f);

                    var markerLabel = CreateText("Player", marker.transform, $"P{m + 1}", 14, Vector2.zero, new Vector2(40f, 40f));
                    var outline = markerLabel.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
                    outline.effectDistance = new Vector2(1f, -1f);

                    marker.gameObject.SetActive(false);
                    markerLabel.gameObject.SetActive(false);

                    markers[m] = marker;
                    labels[m] = markerLabel;
                }

                var slotProperty = slotsProperty.GetArrayElementAtIndex(i);
                var markersProperty = slotProperty.FindPropertyRelative("markers");
                var labelsProperty = slotProperty.FindPropertyRelative("labels");
                markersProperty.arraySize = markers.Length;
                labelsProperty.arraySize = labels.Length;
                for (int m = 0; m < markers.Length; m++)
                {
                    markersProperty.GetArrayElementAtIndex(m).objectReferenceValue = markers[m];
                    labelsProperty.GetArrayElementAtIndex(m).objectReferenceValue = labels[m];
                }
            }

            var colorsProperty = serializedView.FindProperty("colors");
            colorsProperty.arraySize = SwatchColors.Length;
            for (int i = 0; i < SwatchColors.Length; i++)
            {
                colorsProperty.GetArrayElementAtIndex(i).colorValue = SwatchColors[i];
            }

            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        private static LocalMultiplayerPlayerCard CreatePlayerCard(Transform parent, int slot)
        {
            var cardGo = new GameObject($"PlayerCard_P{slot + 1}", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(parent, false);

            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(-690f + slot * 460f, -270f);
            cardRect.sizeDelta = new Vector2(440f, 360f);

            var background = cardGo.GetComponent<Image>();
            background.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            background.raycastTarget = false;

            var playerLabel = CreateText("PlayerLabel", cardGo.transform, $"P{slot + 1}", 34,
                new Vector2(-140f, 140f), new Vector2(120f, 48f));
            playerLabel.alignment = TextAnchor.MiddleLeft;

            var statusLabel = CreateText("StatusLabel", cardGo.transform, "不参加", 22,
                new Vector2(45f, 140f), new Vector2(260f, 40f));
            statusLabel.alignment = TextAnchor.MiddleRight;

            var readyBadge = CreateImage("ReadyBadge", cardGo.transform, new Color(0.08f, 0.08f, 0.08f, 1f), false);
            var badgeRect = readyBadge.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(190f, 140f);
            badgeRect.sizeDelta = new Vector2(18f, 18f);

            CreateText("PositionTitle", cardGo.transform, "位置", 20, new Vector2(-175f, 92f), new Vector2(60f, 28f))
                .alignment = TextAnchor.MiddleLeft;

            var positionButtons = new Button[4];
            for (int i = 0; i < positionButtons.Length; i++)
            {
                positionButtons[i] = CreateButton(
                    $"Position_{i}",
                    (i + 1).ToString(),
                    cardGo.transform,
                    new Vector2(-126f + i * 84f, 52f),
                    new Vector2(66f, 44f),
                    new Color(0.18f, 0.18f, 0.18f, 1f));
            }

            CreateText("ColorTitle", cardGo.transform, "颜色", 20, new Vector2(-175f, 10f), new Vector2(60f, 28f))
                .alignment = TextAnchor.MiddleLeft;

            var colorButtons = new Button[SwatchColors.Length + 1];
            colorButtons[0] = CreateButton(
                "Color_None",
                "不参加",
                cardGo.transform,
                new Vector2(-170f, -30f),
                new Vector2(64f, 36f),
                new Color(0.18f, 0.18f, 0.18f, 1f));

            for (int i = 0; i < SwatchColors.Length; i++)
            {
                colorButtons[i + 1] = CreateColorButton(
                    $"Color_{i}",
                    cardGo.transform,
                    new Vector2(-112f + i * 40f, -30f),
                    SwatchColors[i]);
            }

            var readyButton = CreateButton(
                "ReadyButton",
                "准备",
                cardGo.transform,
                new Vector2(0f, -115f),
                new Vector2(190f, 46f),
                new Color(0.18f, 0.18f, 0.18f, 1f));

            var hintLabel = CreateText("CardHint", cardGo.transform, "确认选择", 18,
                new Vector2(0f, -158f), new Vector2(400f, 28f));

            var card = cardGo.AddComponent<LocalMultiplayerPlayerCard>();
            SetCardFields(card, slot, playerLabel, statusLabel, hintLabel,
                positionButtons, colorButtons, readyButton, readyBadge, background);

            return card;
        }

        private static Button CreateButton(
            string name,
            string label,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            // 菜单按钮放在 HorizontalLayoutGroup 下；没有 LayoutElement 时布局组会忽略 sizeDelta，
            // 首页四个选项会挤在一起，看起来只剩一个按钮。
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.minWidth = size.x;
            layoutElement.minHeight = size.y;
            layoutElement.preferredWidth = size.x;
            layoutElement.preferredHeight = size.y;

            var text = CreateText("Label", go.transform, label, 20, Vector2.zero, size);
            Stretch(text.rectTransform);

            return button;
        }

        private static Button CreateColorButton(string name, Transform parent, Vector2 position, Color swatchColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(36f, 36f);

            var frame = go.GetComponent<Image>();
            frame.color = new Color(0.18f, 0.18f, 0.18f, 1f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;

            var swatch = CreateImage("Swatch", go.transform, swatchColor, false);
            var swatchRect = swatch.rectTransform;
            swatchRect.anchorMin = swatchRect.anchorMax = new Vector2(0.5f, 0.5f);
            swatchRect.anchoredPosition = Vector2.zero;
            swatchRect.sizeDelta = new Vector2(26f, 26f);

            return button;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = GetFont();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;

            Stretch(go.GetComponent<RectTransform>());
            return image;
        }

        private static Font GetFont()
        {
            if (cachedFont != null) return cachedFont;

            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return cachedFont;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetCardFields(
            LocalMultiplayerPlayerCard card,
            int slot,
            Text playerLabel,
            Text statusLabel,
            Text hintLabel,
            Button[] positionButtons,
            Button[] colorButtons,
            Button readyButton,
            Image readyBadge,
            Image background)
        {
            var serialized = new SerializedObject(card);
            serialized.FindProperty("playerSlot").intValue = slot;
            serialized.FindProperty("playerLabel").objectReferenceValue = playerLabel;
            serialized.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            serialized.FindProperty("hintLabel").objectReferenceValue = hintLabel;
            serialized.FindProperty("readyButton").objectReferenceValue = readyButton;
            serialized.FindProperty("readyBadge").objectReferenceValue = readyBadge;
            serialized.FindProperty("background").objectReferenceValue = background;

            var positions = serialized.FindProperty("positionButtons");
            positions.arraySize = positionButtons.Length;
            for (int i = 0; i < positionButtons.Length; i++)
            {
                positions.GetArrayElementAtIndex(i).objectReferenceValue = positionButtons[i];
            }

            var colors = serialized.FindProperty("colorButtons");
            colors.arraySize = colorButtons.Length;
            for (int i = 0; i < colorButtons.Length; i++)
            {
                colors.GetArrayElementAtIndex(i).objectReferenceValue = colorButtons[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureManager(
            LocalMultiplayerSelectionManager manager,
            LocalMultiplayerPlayerCard[] cards,
            Text stageLabel,
            Text countdownLabel,
            Text hintLabel)
        {
            var serialized = new SerializedObject(manager);

            var cardsProperty = serialized.FindProperty("cards");
            cardsProperty.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                cardsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            serialized.FindProperty("positionCount").intValue = 4;
            serialized.FindProperty("colorCount").intValue = SwatchColors.Length;
            serialized.FindProperty("includeKeyboardMouseAsP1").boolValue = true;
            serialized.FindProperty("allowDuplicateSelections").boolValue = false;
            serialized.FindProperty("autoStartWhenAllReady").boolValue = true;
            serialized.FindProperty("countdownSeconds").floatValue = 3f;
            serialized.FindProperty("stageLabel").objectReferenceValue = stageLabel;
            serialized.FindProperty("countdownLabel").objectReferenceValue = countdownLabel;
            serialized.FindProperty("hintLabel").objectReferenceValue = hintLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
