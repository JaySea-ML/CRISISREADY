using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MRCrisisTrainer.Logging
{
    public class JSONLLogger : MonoBehaviour
    {
        public static JSONLLogger Instance { get; private set; }

        [SerializeField] private string fileName = "metrics.jsonl";
        private string filePath;
        private StreamWriter writer;
        private string sessionId;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            filePath = Path.Combine(Application.persistentDataPath, fileName);
            try
            {
                writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
                LogEvent("session_start", new Dictionary<string, object>
                {
                    { "session_id", sessionId },
                    { "device", SystemInfo.deviceModel },
                    { "unity_version", Application.unityVersion },
                    { "platform", Application.platform.ToString() }
                });
                Debug.Log($"[JSONLLogger] Logging to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[JSONLLogger] Cannot open file: {e.Message}");
            }
        }

        public void LogEvent(string eventType, Dictionary<string, object> data)
        {
            if (writer == null) return;
            var entry = new Dictionary<string, object>
            {
                { "ts", DateTime.UtcNow.ToString("o") },
                { "elapsed", Time.realtimeSinceStartup },
                { "session", sessionId },
                { "type", eventType }
            };
            if (data != null)
            {
                foreach (var kv in data) entry[kv.Key] = kv.Value;
            }
            try
            {
                writer.WriteLine(Serialize(entry));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JSONLLogger] Write failed: {e.Message}");
            }
        }

        private static string Serialize(Dictionary<string, object> entry)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in entry)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(kv.Key)).Append("\":");
                AppendValue(sb, kv.Value);
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendValue(System.Text.StringBuilder sb, object value)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case float f: sb.Append(f.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); break;
                case int i: sb.Append(i.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
                default: sb.Append('"').Append(Escape(value.ToString())).Append('"'); break;
            }
        }

        private static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        void OnDestroy()
        {
            if (writer != null)
            {
                LogEvent("session_end", null);
                writer.Flush();
                writer.Close();
                writer = null;
            }
        }
    }
}
