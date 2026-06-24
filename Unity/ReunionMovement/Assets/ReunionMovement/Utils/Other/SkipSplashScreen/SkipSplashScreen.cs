using UnityEngine;
using UnityEngine.Rendering;

#if !UNITY_6000_0_OR_NEWER
public class SkipSplashScreen
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void Run()
    {
        // SplashScreen.Stop 必须在主线程调用，BeforeSplashScreen 回调保证在主线程执行
        SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
    }
}
#endif