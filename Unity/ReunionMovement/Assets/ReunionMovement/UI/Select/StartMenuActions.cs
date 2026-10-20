using UnityEngine;

namespace ReunionMovement.UI.Select
{
    /// <summary>
    /// 开始菜单的退出行为。音量、分辨率、多语言由 SettingsPanelController 在同一页处理。
    /// </summary>
    public class StartMenuActions : MonoBehaviour
    {
        [SerializeField] private CarouselMenuStack stack;

        private void Awake()
        {
            if (stack == null)
            {
                stack = GetComponentInParent<CarouselMenuStack>();
            }
        }

        private void OnEnable()
        {
            if (stack != null) stack.onCompleted += OnCompleted;
        }

        private void OnDisable()
        {
            if (stack != null) stack.onCompleted -= OnCompleted;
        }

        private void OnCompleted()
        {
            QuitGame();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}