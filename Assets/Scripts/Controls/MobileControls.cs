using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Shows the on-screen controls on phones and tablets (including the browser
    /// version on iPhone) and hides them on desktop builds.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        [Tooltip("Show the touch controls when playing in the Unity editor (handy with Unity Remote).")]
        public bool showInEditor = true;

        [Tooltip("Always show the touch controls, even on desktop.")]
        public bool alwaysShow = false;

        void Awake()
        {
            bool show = alwaysShow
                        || Application.isMobilePlatform
                        || (Application.isEditor && showInEditor);
            gameObject.SetActive(show);
        }
    }
}
