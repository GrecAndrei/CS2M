using System;

namespace CS2M.API
{
    /// <summary>
    ///     Decoupled registry for broadcasting cooperative activities from BaseGame/API layer to Mod layer
    /// </summary>
    public static class CooperativeActivityRegistry
    {
        public static Action<string, string, float, float, float> OnActivityRegistered;

        public static void RegisterActivity(string username, string actionText, float x, float y, float z)
        {
            OnActivityRegistered?.Invoke(username, actionText, x, y, z);
        }
    }
}
