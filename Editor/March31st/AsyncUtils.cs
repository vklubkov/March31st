using UnityEditor;
using UnityEngine;

namespace March31st {
    public static class AsyncUtils {
        public static Awaitable Yield() {
            if (Application.isPlaying)
                return Awaitable.NextFrameAsync();

            AwaitableCompletionSource completionSource = new();
            EditorApplication.delayCall += () => completionSource.SetResult();
            return completionSource.Awaitable;
        }
    }
}