using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Research;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// Tworzy wszystkie assety danych badawczych: ScenarioConfig (mikrokroki 3 aktów)
    /// oraz QuestionnaireDefinition (NASA-TLX, SUS, UEQ-S, SSQ, IPQ, SAM) z realnymi pozycjami.
    /// </summary>
    public static class ResearchDataBuilder
    {
        private const string ScenDir = "Assets/_Project/ScriptableObjects/Scenarios";
        private const string QDir = "Assets/_Project/ScriptableObjects/Questionnaires";

        [MenuItem("MRCrisis/Build Research Data Assets", priority = 30)]
        public static void BuildAll()
        {
            EnsureDir(ScenDir);
            EnsureDir(QDir);
            BuildScenarios();
            BuildQuestionnaires();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ResearchDataBuilder] All research data assets created.");
        }

        // ---------------- SCENARIOS ----------------

        private static void BuildScenarios()
        {
            // AKT II - Samochód i poślizg
            CreateScenario("act2_skid", "Akt II: Poślizg",
                "Jedziesz samochodem. Auto wpada w poślizg — odzyskaj kontrolę.",
                new[]
                {
                    // ~2 minuty: spokojna jazda → POŚLIZG W PRAWO → recovery → jazda → POŚLIZG W LEWO → recovery → powrót.
                    Step("drive_calm", "Jedź spokojnie prawym pasem", "Trzymaj prawy pas obiema rękami na kierownicy. Za chwilę droga zrobi się śliska.", 6, 26),
                    Step("recognize_skid", "POŚLIZG W PRAWO — rozpoznaj", "Tył auta ucieka w prawo. Za moment łap kierownicę.", 3, 12),
                    Step("grip_wheel", "Chwyć kierownicę OBIEMA rękami", "Złap kierownicę pewnie obiema dłońmi.", 4, 20),
                    Step("counter_steer", "Kontrasteruj — skręć w stronę poślizgu", "Skręcaj zdecydowanie w prawo, aż auto się wyprostuje.", 3, 25),
                    Step("stabilize", "Ustabilizuj tor jazdy", "Trzymaj prosto — auto wraca na kurs.", 4, 12),
                    Step("drive_between", "Jedź dalej — uwaga na drogę", "Dobrze! Jedź spokojnie. Bądź gotów na kolejny poślizg.", 8, 40),
                    Step("recognize_skid_l", "POŚLIZG W LEWO — rozpoznaj", "Tył auta ucieka w lewo. Za moment łap kierownicę.", 3, 12),
                    Step("counter_steer_l", "Kontrasteruj — skręć w stronę poślizgu", "Skręcaj zdecydowanie w lewo, aż auto się wyprostuje.", 3, 25),
                    Step("stabilize_l", "Ustabilizuj tor jazdy", "Trzymaj prosto — auto wraca na kurs.", 4, 12),
                    Step("resume", "Wróć do bezpiecznej jazdy", "Świetnie! Jedź spokojnie — za chwilę zadzwoni telefon.", 6, 18),
                    Step("answer_phone", "ODBIERZ TELEFON — spójrz w PRAWO", "Telefon dzwoni na siedzeniu pasażera. Sięgnij ręką i złap słuchawkę.", 2, 45),
                });

            // AKT III - Telefon, ukrycie POD ŁÓŻKIEM, rozmowa z 112, cisza
            // Sekwencja: odbierz telefon → wczołgaj się pod łóżko → przeczytaj formułki do 112 →
            // dyspozytor mówi, że jedzie patrol → wchodzi intruz → leż cicho 2 min do syren.
            CreateScenario("act3_call", "Akt III: Telefon i ukrycie",
                "Słyszysz włamywacza. Odbierz telefon, wczołgaj się pod łóżko, zadzwoń na 112 i zachowaj ciszę.",
                new[]
                {
                    Step("grab_phone", "Odbierz dzwoniący telefon", "Złap słuchawkę dzwoniącego telefonu ze stolika.", 6, 30),
                    Step("hide_under_bed", "Wczołgaj się POD ŁÓŻKO", "Podejdź do łóżka i zejdź nisko — wczołgaj się pod nie i schowaj przy podłodze.", 6, 60),
                    Step("give_location", "Powiedz: \"Halo, 112? Włamanie. Ulica Długa 12, mieszkanie 5, drugie piętro.\"", "Mów spokojnie i wyraźnie do słuchawki.", 1.5f, 40),
                    Step("describe_event", "Powiedz: \"Ktoś jest w moim mieszkaniu. Schowałem się pod łóżkiem w sypialni.\"", "Mów cicho — włamywacz nie może Cię usłyszeć.", 1.5f, 40),
                    Step("count_victims", "Powiedz: \"Jestem sam, nie ma ze mną nikogo innego.\"", "Mów spokojnie.", 1.5f, 35),
                    Step("give_status", "Powiedz: \"Słyszę kroki. Proszę szybko o pomoc.\"", "Po tym dyspozytor wyśle patrol.", 1.5f, 35),
                    Step("stay_silent", "LEŻ CICHO — patrol jest w drodze", "Włamywacz przeszukuje pokój. Nie ruszaj się i nie wydawaj dźwięku. Czekaj na syreny.", 999, 150),
                });
        }

        private static Microstep Step(string id, string label, string hint, float hintDelay, float timeout)
        {
            return new Microstep
            {
                id = id, label = label, hintText = hint,
                hintDelaySeconds = hintDelay, timeoutSeconds = timeout,
                isMandatory = true, isBlocking = true
            };
        }

        private static void CreateScenario(string id, string name, string desc, Microstep[] steps)
        {
            var path = $"{ScenDir}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ScenarioConfig>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<ScenarioConfig>(); AssetDatabase.CreateAsset(asset, path); }
            asset.scenarioId = id;
            asset.displayName = name;
            asset.description = desc;
            asset.microsteps = new List<Microstep>(steps);
            asset.defaultScaffolding = ScaffoldingLevel.Full;
            asset.successThreshold = 0.7f;
            EditorUtility.SetDirty(asset);
        }

        // ---------------- QUESTIONNAIRES ----------------

        private static void BuildQuestionnaires()
        {
            // NASA-TLX (6 podskal, 0-20 reprezentuje 0-100)
            CreateQ("nasa_tlx", "NASA-TLX (obciążenie zadaniem)",
                "Oceń każdy wymiar od niskiego (0) do wysokiego (20).", ScaleType.Scale0to20,
                new[]
                {
                    Q("mental", "Obciążenie umysłowe: jak bardzo wymagające umysłowo było zadanie?", "Niskie", "Wysokie"),
                    Q("physical", "Obciążenie fizyczne: jak bardzo wymagające fizycznie?", "Niskie", "Wysokie"),
                    Q("temporal", "Presja czasu: jak bardzo czułeś presję tempa?", "Niska", "Wysoka"),
                    Q("performance", "Wydajność: jak skuteczny byłeś w realizacji?", "Doskonała", "Słaba"),
                    Q("effort", "Wysiłek: ile musiałeś włożyć pracy?", "Mały", "Duży"),
                    Q("frustration", "Frustracja: jak bardzo byłeś zniechęcony/zestresowany?", "Mała", "Duża"),
                });

            // SUS (10 pozycji, 1-5, parzyste odwrócone)
            CreateQ("sus", "SUS (użyteczność systemu)",
                "Oceń zgodność: 1 = zdecydowanie się nie zgadzam, 5 = zdecydowanie się zgadzam.", ScaleType.Likert5,
                new[]
                {
                    Q("sus1", "Chętnie korzystałbym z tego systemu często.", "Nie zgadzam się", "Zgadzam się"),
                    QR("sus2", "System był niepotrzebnie skomplikowany.", "Nie zgadzam się", "Zgadzam się"),
                    Q("sus3", "System był łatwy w użyciu.", "Nie zgadzam się", "Zgadzam się"),
                    QR("sus4", "Potrzebowałbym wsparcia technicznego, by go używać.", "Nie zgadzam się", "Zgadzam się"),
                    Q("sus5", "Funkcje systemu były dobrze zintegrowane.", "Nie zgadzam się", "Zgadzam się"),
                    QR("sus6", "System był zbyt niespójny.", "Nie zgadzam się", "Zgadzam się"),
                    Q("sus7", "Większość osób szybko nauczy się systemu.", "Nie zgadzam się", "Zgadzam się"),
                    QR("sus8", "System był uciążliwy w użyciu.", "Nie zgadzam się", "Zgadzam się"),
                    Q("sus9", "Czułem się pewnie, korzystając z systemu.", "Nie zgadzam się", "Zgadzam się"),
                    QR("sus10", "Musiałem się wiele nauczyć, zanim zacząłem.", "Nie zgadzam się", "Zgadzam się"),
                });

            // UEQ-S (8 pozycji, 1-7 semantic differential)
            CreateQ("ueq_s", "UEQ-S (doświadczenie użytkownika)",
                "Wybierz wartość bliższą określeniu które pasuje (1-7).", ScaleType.Likert7Semantic,
                new[]
                {
                    Q("ueq1", "Ogólne wrażenie wsparcia", "Przeszkadzający", "Wspierający"),
                    Q("ueq2", "Złożoność obsługi", "Skomplikowany", "Prosty"),
                    Q("ueq3", "Efektywność", "Nieefektywny", "Efektywny"),
                    Q("ueq4", "Przejrzystość", "Mylący", "Przejrzysty"),
                    Q("ueq5", "Pobudzenie", "Nudny", "Ekscytujący"),
                    Q("ueq6", "Zainteresowanie", "Nieciekawy", "Interesujący"),
                    Q("ueq7", "Innowacyjność", "Konwencjonalny", "Nowatorski"),
                    Q("ueq8", "Kreatywność", "Zwyczajny", "Twórczy"),
                });

            // SSQ (kluczowe objawy, 0-3)
            CreateQ("ssq", "SSQ (objawy cybersickness)",
                "Oceń nasilenie każdego objawu: 0 = brak, 1 = lekkie, 2 = umiarkowane, 3 = silne.", ScaleType.Severity0to3,
                new[]
                {
                    Q("general_discomfort", "Ogólny dyskomfort", "Brak", "Silne"),
                    Q("fatigue", "Zmęczenie", "Brak", "Silne"),
                    Q("headache", "Ból głowy", "Brak", "Silne"),
                    Q("eyestrain", "Zmęczenie oczu", "Brak", "Silne"),
                    Q("difficulty_focusing", "Trudność z ostrością widzenia", "Brak", "Silne"),
                    Q("nausea", "Mdłości", "Brak", "Silne"),
                    Q("dizziness", "Zawroty głowy", "Brak", "Silne"),
                    Q("vertigo", "Uczucie wirowania", "Brak", "Silne"),
                });

            // IPQ (poczucie obecności, 1-7)
            CreateQ("ipq", "IPQ (poczucie obecności)",
                "Oceń zgodność od 1 (nie) do 7 (tak).", ScaleType.Likert7,
                new[]
                {
                    Q("ipq_g1", "Miałem poczucie, że jestem w wirtualnym świecie.", "Nie", "Tak"),
                    Q("ipq_sp1", "Czułem, że obiekty wirtualne są wokół mnie.", "Nie", "Tak"),
                    Q("ipq_sp2", "Miałem wrażenie działania w wirtualnej przestrzeni.", "Nie", "Tak"),
                    Q("ipq_sp3", "Wirtualny świat wydawał się realniejszy niż obraz.", "Nie", "Tak"),
                    Q("ipq_inv1", "Byłem pochłonięty wirtualnym światem.", "Nie", "Tak"),
                    Q("ipq_inv2", "Nie zwracałem uwagi na realne otoczenie.", "Nie", "Tak"),
                    Q("ipq_real1", "Wirtualny świat wydawał się autentyczny.", "Nie", "Tak"),
                    Q("ipq_real2", "Reakcje obiektów były realistyczne.", "Nie", "Tak"),
                });

            // SAM (3 wymiary emocji, 1-9)
            CreateQ("sam", "SAM (samoocena emocji)",
                "Oceń swój stan emocjonalny po sesji (1-9).", ScaleType.SAM1to9,
                new[]
                {
                    Q("valence", "Nastrój (przyjemność)", "Nieprzyjemny", "Przyjemny"),
                    Q("arousal", "Pobudzenie", "Spokojny", "Pobudzony"),
                    Q("dominance", "Poczucie kontroli", "Uległy", "Dominujący"),
                });
        }

        private static QuestionItem Q(string id, string text, string left, string right) =>
            new QuestionItem { id = id, text = text, leftAnchor = left, rightAnchor = right, reverseScored = false };

        private static QuestionItem QR(string id, string text, string left, string right) =>
            new QuestionItem { id = id, text = text, leftAnchor = left, rightAnchor = right, reverseScored = true };

        private static void CreateQ(string id, string title, string instr, ScaleType scale, QuestionItem[] items)
        {
            var path = $"{QDir}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<QuestionnaireDefinition>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<QuestionnaireDefinition>(); AssetDatabase.CreateAsset(asset, path); }
            asset.questionnaireId = id;
            asset.title = title;
            asset.instructions = instr;
            asset.scaleType = scale;
            asset.items = new List<QuestionItem>(items);
            EditorUtility.SetDirty(asset);
        }

        private static void EnsureDir(string dir)
        {
            var parts = dir.Split('/');
            var path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{path}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }
    }
}
