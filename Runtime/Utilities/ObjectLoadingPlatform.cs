namespace Deucarian.ObjectLoading
{
    public static class ObjectLoadingPlatform
    {
        public static string GetCurrentPlatformName()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return "android";
#elif UNITY_IOS && !UNITY_EDITOR
            return "ios";
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return "windows";
#elif UNITY_WEBGL
            return "webgl";
#else
            return "webgl";
#endif
        }
    }
}
