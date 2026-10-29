using ReunionMovement.Common;
using ReunionMovement.Framework.Runtime;
using ReunionMovement.Framework.Runtime.UIInput;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.UI.Select
{
    /// <summary>
    /// 单页设置面板：主音量、分辨率、全屏、多语言放在同一界面。
    /// 键盘/手柄/方向盘用上下切换行，左右调整值；确认切换全屏；取消返回首页。
    /// </summary>
    public class SettingsPanelController : MonoBehaviour, IUINavigationTarget
    {
        [SerializeField] private CarouselMenuStack stack;
        [SerializeField] private int homeLevel = 1;

        [Header("控件")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValueLabel;
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Dropdown languageDropdown;
        [SerializeField] private Button backButton;

        [Header("行高亮")]
        [SerializeField] private Image[] rowBackgrounds;
        [SerializeField] private Color rowNormalColor = new Color(0.11f, 0.11f, 0.13f, 1f);
        [SerializeField] private Color rowSelectedColor = new Color(0.20f, 0.24f, 0.30f, 1f);

        [Header("分辨率")]
        [SerializeField] private int[] resolutionWidths = { 1280, 1600, 1920 };
        [SerializeField] private int[] resolutionHeights = { 720, 900, 1080 };

        [Header("语言")]
        [SerializeField] private string[] languageNames = { "中文", "English", "Русский", "日本語" };

        private int currentRow;
        private bool registered;
        private bool bindRequested;
        private bool suppressCallbacks;
        private int lastInitVersion = -1;

        public int InputPriority => 120;
        public int PlayerSlot => 0;

        private void Awake()
        {
            if (stack == null) stack = GetComponentInParent<CarouselMenuStack>();
            GameOption.LoadOptions();

            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (languageDropdown != null) languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
            if (backButton != null) backButton.onClick.AddListener(BackToHome);
        }

        private void OnEnable()
        {
            LoadSettings();
            RefreshHighlight();
            if (!UIInputSystem.Instance.isInited)
            {
                bindRequested = true;
                return;
            }
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            if (languageDropdown != null) languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
            if (backButton != null) backButton.onClick.RemoveListener(BackToHome);
        }

        private void Update()
        {
            var inputSystem = UIInputSystem.Instance;
            if (!inputSystem.isInited) return;

            int version = inputSystem.InitVersion;
            if (bindRequested || lastInitVersion != version)
            {
                lastInitVersion = version;
                Bind();
            }
        }

        private void Bind()
        {
            Unbind();
            if (!UIInputSystem.Instance.isInited)
            {
                bindRequested = true;
                return;
            }

            bindRequested = false;
            UIInputSystem.Instance.RegisterNavigationTarget(this, true);
            registered = true;
        }

        private void Unbind()
        {
            bindRequested = false;
            if (!registered) return;
            UIInputSystem.Instance.UnregisterNavigationTarget(this);
            registered = false;
        }

        public bool AcceptsPlayer(int playerIndex)
        {
            return playerIndex == PlayerSlot;
        }

        public bool HandleNavigate(in UINavigateEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex) || !isActiveAndEnabled) return false;

            if (e.IsWheelSteering)
            {
                return Adjust(e.Step.x);
            }

            if (e.Step.y != 0)
            {
                MoveRow(e.Step.y > 0 ? -1 : 1);
                return true;
            }

            if (e.Step.x != 0)
            {
                return Adjust(e.Step.x);
            }

            return false;
        }

        public bool HandleSubmit(in UIButtonEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex) || !isActiveAndEnabled) return false;

            if (currentRow == 2 && fullscreenToggle != null)
            {
                ChangeFullscreen(!fullscreenToggle.isOn);
            }
            return true;
        }

        public bool HandleCancel(in UIButtonEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex) || !isActiveAndEnabled) return false;
            BackToHome();
            return true;
        }

        /// <summary>返回首页；鼠标点击返回按钮和取消键共用。</summary>
        public void BackToHome()
        {
            if (stack != null)
            {
                stack.OpenLevel(homeLevel);
            }
        }

        private void MoveRow(int delta)
        {
            int count = rowBackgrounds != null ? rowBackgrounds.Length : 4;
            if (count <= 0) return;

            currentRow = (currentRow + delta + count) % count;
            RefreshHighlight();
        }

        private bool Adjust(int direction)
        {
            if (direction == 0) return false;

            switch (currentRow)
            {
                case 0:
                    if (volumeSlider == null) return false;
                    ChangeVolume(volumeSlider.value + direction * 0.05f);
                    return true;

                case 1:
                    if (resolutionDropdown == null) return false;
                    ChangeResolution(resolutionDropdown.value + direction);
                    return true;

                case 2:
                    if (fullscreenToggle == null) return false;
                    ChangeFullscreen(!fullscreenToggle.isOn);
                    return true;

                case 3:
                    if (languageDropdown == null) return false;
                    ChangeLanguage(languageDropdown.value + direction);
                    return true;
            }

            return false;
        }

        private void ChangeVolume(float value)
        {
            if (volumeSlider == null) return;
            volumeSlider.SetValueWithoutNotify(Mathf.Clamp01(value));
            ApplyVolume(volumeSlider.value);
        }

        private void ChangeResolution(int index)
        {
            if (resolutionDropdown == null || resolutionDropdown.options.Count == 0) return;
            index = Mathf.Clamp(index, 0, resolutionDropdown.options.Count - 1);
            resolutionDropdown.SetValueWithoutNotify(index);
            resolutionDropdown.RefreshShownValue();
            ApplyResolution(index);
        }

        private void ChangeFullscreen(bool value)
        {
            if (fullscreenToggle == null) return;
            suppressCallbacks = true;
            fullscreenToggle.isOn = value;
            suppressCallbacks = false;
            ApplyFullscreen(value);
        }

        private void ChangeLanguage(int index)
        {
            if (languageDropdown == null || languageDropdown.options.Count == 0) return;
            index = Mathf.Clamp(index, 0, languageDropdown.options.Count - 1);
            languageDropdown.SetValueWithoutNotify(index);
            languageDropdown.RefreshShownValue();
            ApplyLanguage(index);
        }

        private void OnVolumeChanged(float value)
        {
            if (suppressCallbacks) return;
            ApplyVolume(value);
        }

        private void OnResolutionChanged(int index)
        {
            if (suppressCallbacks) return;
            ApplyResolution(index);
        }

        private void OnFullscreenChanged(bool value)
        {
            if (suppressCallbacks) return;
            ApplyFullscreen(value);
        }

        private void OnLanguageChanged(int index)
        {
            if (suppressCallbacks) return;
            ApplyLanguage(index);
        }

        private void ApplyVolume(float value)
        {
            value = Mathf.Clamp01(value);
            GameOption.SetMasterVolume(value);

            if (volumeValueLabel != null)
            {
                volumeValueLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
            }
        }

        private void ApplyResolution(int index)
        {
            if (resolutionWidths == null || resolutionHeights == null ||
                index < 0 || index >= resolutionWidths.Length || index >= resolutionHeights.Length)
            {
                return;
            }

            var option = GameOption.CurrentOption;
            option.resolutionWidth = resolutionWidths[index];
            option.resolutionHeight = resolutionHeights[index];

            // 复用 GameOption 的显示应用路径，自动处理平台限制和持久化。
            GameOption.SetFullscreen(fullscreenToggle != null && fullscreenToggle.isOn);
        }

        private void ApplyFullscreen(bool value)
        {
            GameOption.SetFullscreen(value);
        }

        private void ApplyLanguage(int index)
        {
            if (languageNames == null || index < 0 || index >= languageNames.Length) return;

            GameOption.SetLanguage((Multilingual)index);
        }

        private void LoadSettings()
        {
            suppressCallbacks = true;

            var option = GameOption.CurrentOption;

            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(Mathf.Clamp01(option.masterVolume));
            }

            if (resolutionDropdown != null && resolutionDropdown.options.Count > 0)
            {
                int resolution = FindResolutionIndex(option.resolutionWidth, option.resolutionHeight);
                resolutionDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(resolution, 0, resolutionDropdown.options.Count - 1));
                resolutionDropdown.RefreshShownValue();
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = option.fullscreen;
            }

            if (languageDropdown != null && languageDropdown.options.Count > 0)
            {
                int language = (int)option.language;
                languageDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(language, 0, languageDropdown.options.Count - 1));
                languageDropdown.RefreshShownValue();
            }

            suppressCallbacks = false;

            if (volumeSlider != null)
            {
                ApplyVolume(volumeSlider.value);
            }
        }

        private int FindResolutionIndex(int width, int height)
        {
            if (resolutionWidths == null || resolutionHeights == null) return 0;

            int count = Mathf.Min(resolutionWidths.Length, resolutionHeights.Length);
            for (int i = 0; i < count; i++)
            {
                if (resolutionWidths[i] == width && resolutionHeights[i] == height)
                {
                    return i;
                }
            }
            return 0;
        }

        private void RefreshHighlight()
        {
            if (rowBackgrounds == null) return;

            for (int i = 0; i < rowBackgrounds.Length; i++)
            {
                if (rowBackgrounds[i] != null)
                {
                    rowBackgrounds[i].color = i == currentRow ? rowSelectedColor : rowNormalColor;
                }
            }
        }
    }
}
