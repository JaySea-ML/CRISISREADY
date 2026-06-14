using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRCrisisTrainer.Research
{
    public enum ScaleType
    {
        Likert5,            // 1-5 (SUS)
        Likert7,            // 1-7 (IPQ, UEQ-S)
        Likert7Semantic,    // 1-7 z dwoma biegunami (UEQ-S)
        Scale0to20,         // NASA-TLX (0-100 w krokach co 5 -> 21 punktów)
        Severity0to3,       // SSQ (none/slight/moderate/severe)
        SAM1to9             // Self-Assessment Manikin (1-9)
    }

    [Serializable]
    public class QuestionItem
    {
        public string id;
        [TextArea(1, 3)] public string text;
        public string leftAnchor;   // np. "Niskie"
        public string rightAnchor;  // np. "Wysokie"
        public bool reverseScored;  // dla pozycji odwróconych (SUS parzyste)
    }

    /// <summary>
    /// Definicja kwestionariusza badawczego (NASA-TLX, SUS, UEQ-S, SSQ, IPQ, SAM).
    /// Prezentowany w VR jako world-space UI, wyniki eksportowane do JSONL.
    /// </summary>
    [CreateAssetMenu(fileName = "Questionnaire", menuName = "MRCrisis/Questionnaire", order = 6)]
    public class QuestionnaireDefinition : ScriptableObject
    {
        public string questionnaireId;
        public string title;
        [TextArea(2, 4)] public string instructions;
        public ScaleType scaleType = ScaleType.Likert7;
        public List<QuestionItem> items = new List<QuestionItem>();
    }
}
