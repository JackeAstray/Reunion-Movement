// Unity ↔ 浏览器（宿主页面）桥接库。
// 全屏控制（C# 入口：StartGame.SetFullscreen）：
//   SetFullscreenState(value)           —— 非 0 进入全屏，0 退出全屏
//   GetFullscreenState()                —— 当前是否全屏（1/0）
//   RegisterFullscreenChanged(callback) —— 注册全屏状态变化回调（参数：1=全屏，0=非全屏）
//
// 实现说明：
// - 全屏目标 = canvas 的父容器（#unity-container，无父节点时退回 body）；
// - 浏览器只保证「全屏元素本身」铺满屏幕，画布的内联固定尺寸（960x600）不会自动拉伸，
//   因此进入全屏后把画布拉伸为 100%，退出（含用户按 Esc）时还原原内联样式；
// - requestFullscreen/exitFullscreen 返回的 Promise 被拒绝（无用户手势、权限策略）时
//   仅打印警告，避免未处理的 Promise 异常；
// - 兼容 webkit / moz / ms 前缀；fullscreenchange 监听只注册一次，覆盖 Esc、
//   页面按钮等所有退出方式（回调 C# 用 makeDynCall，同 WebGLInput.jslib 的用法）。

var UnityToWeb =
{
    // 全屏共享状态与方法（$ 前缀：库内部符号；文末 autoAddDeps 注册依赖，避免被构建裁剪）
    $unityFullscreen:
    {
        canvas: null,     // 由本库进入全屏时的画布引用；null = 当前全屏并非本库触发
        prevWidth: "",    // 进入全屏前的画布内联宽度样式（退出时还原）
        prevHeight: "",   // 进入全屏前的画布内联高度样式（退出时还原）
        listening: false, // fullscreenchange 监听是否已注册（只注册一次）
        callback: 0,      // C# 全屏变化回调（wasm 函数指针，0 = 未注册）

        // 当前是否有元素处于全屏（兼容各浏览器前缀）
        isActive: function ()
        {
            var doc = document;
            return !!(doc.fullscreenElement || doc.webkitFullscreenElement ||
                      doc.mozFullScreenElement || doc.msFullscreenElement);
        },

        // 注册 fullscreenchange 监听（各前缀，进程内仅一次）
        ensureListeners: function ()
        {
            if (unityFullscreen.listening) return;
            unityFullscreen.listening = true;

            var doc = document;
            doc.addEventListener('fullscreenchange', unityFullscreen.onChange, false);
            doc.addEventListener('webkitfullscreenchange', unityFullscreen.onChange, false);
            doc.addEventListener('mozfullscreenchange', unityFullscreen.onChange, false);
            doc.addEventListener('MSFullscreenChange', unityFullscreen.onChange, false);
        },

        // 全屏变化回调：
        // 1) 画布样式：只处理由本库发起的全屏（canvas 非空），进入时铺满容器、退出时还原；
        // 2) 通知 C#（已注册时）：进入/退出全屏的统一出口，含 Esc 与页面按钮触发的变化。
        onChange: function ()
        {
            var active = unityFullscreen.isActive();

            if (unityFullscreen.canvas)
            {
                if (active)
                {
                    unityFullscreen.canvas.style.width = '100%';
                    unityFullscreen.canvas.style.height = '100%';
                }
                else
                {
                    unityFullscreen.canvas.style.width = unityFullscreen.prevWidth;
                    unityFullscreen.canvas.style.height = unityFullscreen.prevHeight;
                    unityFullscreen.canvas = null;
                    unityFullscreen.prevWidth = "";
                    unityFullscreen.prevHeight = "";
                }
            }

            var cb = unityFullscreen.callback;
            if (cb)
            {
                try
                {
                    {{{ makeDynCall('vi', 'cb') }}}(active ? 1 : 0);
                }
                catch (e)
                {
                    console.warn("[UnityToWeb] 全屏状态回调异常: " + e);
                }
            }
        },

        // 请求被拒绝时的告警（结果可能为 Promise；老浏览器返回 undefined）
        warnRejected: function (result, action)
        {
            if (result && typeof result.catch === 'function')
            {
                result.catch(function (e) { console.warn("[UnityToWeb] " + action + "失败: " + e); });
            }
        },
    },

    SendStr: function (value) 
    {
        SendString(UTF8ToString(value));
    },

    // C# 入口：非 0 进入全屏，0 退出全屏
    SetFullscreenState: function (value) 
    {
        try
        {
            var doc = document;

            if (value === 0)
            {
                // ---- 退出全屏（当前未处于全屏时为空操作）----
                if (!unityFullscreen.isActive()) return;

                var exit = doc.exitFullscreen || doc.webkitExitFullscreen ||
                           doc.msExitFullscreen || doc.mozCancelFullScreen;
                if (exit) unityFullscreen.warnRejected(exit.call(doc), "退出全屏");
                return;
            }

            // ---- 进入全屏 ----
            // 已在全屏（含页面其他按钮触发）时不重复请求，避免全屏元素被中途切换
            if (unityFullscreen.isActive()) return;

            var canvas = doc.getElementsByTagName('canvas')[0] || null;
            // 全屏目标：canvas 的父容器（#unity-container），无父节点时退回 body
            var container = (canvas && canvas.parentNode) ? canvas.parentNode : doc.body;
            if (!container)
            {
                console.warn("[UnityToWeb] 进入全屏失败：未找到全屏目标元素");
                return;
            }

            // 记录画布内联样式：浏览器只保证全屏元素本身铺满，画布需显式拉伸并在退出时还原
            if (canvas)
            {
                unityFullscreen.canvas = canvas;
                unityFullscreen.prevWidth = canvas.style.width;
                unityFullscreen.prevHeight = canvas.style.height;
            }

            // 用户按 Esc/页面按钮退出时同样依赖该监听还原画布样式并回调 C#
            unityFullscreen.ensureListeners();

            var request = container.requestFullscreen || container.webkitRequestFullscreen ||
                          container.msRequestFullscreen || container.mozRequestFullScreen;
            if (!request)
            {
                console.warn("[UnityToWeb] 进入全屏失败：当前浏览器不支持元素级全屏");
                return;
            }
            unityFullscreen.warnRejected(request.call(container), "进入全屏");
        }
        catch (e)
        {
            console.warn("[UnityToWeb] SetFullscreenState 异常: " + e);
        }
    },

    // C# 入口：当前是否全屏（1/0）
    GetFullscreenState: function () 
    {
        try
        {
            return unityFullscreen.isActive() ? 1 : 0;
        }
        catch (e)
        {
            return 0;
        }
    },

    // C# 入口：注册全屏状态变化回调（callback 为 Action<int> 的 wasm 函数指针）
    RegisterFullscreenChanged: function (callback) 
    {
        try
        {
            unityFullscreen.callback = callback;
            // 尽早开始监听：注册后即使全屏由页面按钮等外部方式触发，也能把状态同步回 C#
            unityFullscreen.ensureListeners();
        }
        catch (e)
        {
            console.warn("[UnityToWeb] RegisterFullscreenChanged 异常: " + e);
        }
    },
}

autoAddDeps(UnityToWeb, '$unityFullscreen');
mergeInto(LibraryManager.library, UnityToWeb);