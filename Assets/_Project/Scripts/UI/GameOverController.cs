using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace MRCrisisTrainer.UI
{
    /// <summary>
    /// Kontroler OSOBNEJ czarnej sceny „GameOver" (koniec gry). Czyta z PlayerPrefs:
    ///  - string „end_message" — duży napis (np. „UDAŁO SIĘ PRZEJŚĆ GRĘ" / „PRZEGRAŁEŚ"),
    ///  - int „end_is_win" (1 = wygrana → zielony, 0 = przegrana → czerwony).
    /// Pokazuje napis na czarnym tle + przycisk „SPRÓBUJ PONOWNIE", który klika się DŁONIĄ
    /// (laser z palca + pinch — jak w menu). Przycisk woła Retry(): ustawia „retry_room_act"=1
    /// i wraca do sceny „TrainingRoom" (SessionFlowManager wznawia od aktu z pokojem).
    /// </summary>
    public class GameOverController : MonoBehaviour
    {
        [Tooltip("Duży napis z wynikiem (end_message). Kolor ustawiany w kodzie.")]
        [SerializeField] private TMP_Text messageLabel;
        [Tooltip("Mniejszy napis-podpowiedź (wskaż przycisk dłonią i ściśnij palce).")]
        [SerializeField] private TMP_Text hintLabel;

        private static readonly Color WinColor = new Color(0.30f, 1f, 0.45f);
        private static readonly Color LossColor = new Color(1f, 0.25f, 0.20f);
        private const string HintText = "Wskaż przycisk dłonią i ściśnij palce";

        private bool retried;

        void Start()
        {
            string message = PlayerPrefs.GetString("end_message", "KONIEC");
            bool isWin = PlayerPrefs.GetInt("end_is_win", 0) == 1;

            if (messageLabel != null)
            {
                messageLabel.text = message;
                messageLabel.color = isWin ? WinColor : LossColor;
                messageLabel.fontStyle = FontStyles.Bold;
                messageLabel.alignment = TextAlignmentOptions.Center;
            }
            if (hintLabel != null)
            {
                hintLabel.text = HintText;
                hintLabel.alignment = TextAlignmentOptions.Center;
            }
        }

        /// <summary>Wołane przez przycisk „SPRÓBUJ PONOWNIE" (klik dłonią). Wraca do gry od aktu z pokojem.</summary>
        public void Retry()
        {
            if (retried) return;
            retried = true;
            PlayerPrefs.SetInt("retry_room_act", 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene("TrainingRoom");
        }
    }
}
