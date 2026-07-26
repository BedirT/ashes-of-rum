using System;

namespace AshesOfRum
{
    public static class HarnessContract
    {
        public const string SceneName = "Bootstrap";
        public const string RootObjectName = "Harness Root";
        public const string CameraObjectName = "Main Camera";

        public static bool HasRequiredObjects(Func<string, bool> objectExists)
        {
            return objectExists(RootObjectName) && objectExists(CameraObjectName);
        }
    }
}
