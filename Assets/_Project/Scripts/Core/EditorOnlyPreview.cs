using UnityEngine;

namespace MRCrisisTrainer.Core
{
    /// <summary>
    /// Obiekty z tym komponentem są widoczne w Editor scene view, ale wyłączają się
    /// w Play Mode lub gdy passthrough jest aktywny - tak żeby w MR widzieć tylko
    /// wirtualne obiekty na tle realnego pokoju.
    /// </summary>
    public class EditorOnlyPreview : MonoBehaviour
    {
        void Awake()
        {
            // In runtime/Play, hide preview objects so passthrough background shows the real room.
            gameObject.SetActive(false);
        }
    }
}
