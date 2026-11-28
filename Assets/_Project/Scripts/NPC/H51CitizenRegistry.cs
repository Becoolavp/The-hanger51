using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51CitizenRegistry : MonoBehaviour
    {
        [Serializable]
        sealed class CitizenSave
        {
            public string citizenId;
            public List<H51CitizenMemory> memories = new List<H51CitizenMemory>();
            public List<H51CitizenRelationship> relationships = new List<H51CitizenRelationship>();
        }

        [Serializable]
        sealed class SavePayload
        {
            public int version = 1;
            public List<CitizenSave> citizens = new List<CitizenSave>();
        }

        public static H51CitizenRegistry Instance { get; private set; }

        [SerializeField] List<H51CitizenIdentity> citizens = new List<H51CitizenIdentity>();
        [SerializeField] List<H51CitizenAnchor> anchors = new List<H51CitizenAnchor>();
        [SerializeField, Min(5f)] float autosaveSeconds = 30f;

        readonly Dictionary<string,H51CitizenIdentity> citizenById = new Dictionary<string,H51CitizenIdentity>();
        readonly Dictionary<string,H51CitizenAnchor> anchorById = new Dictionary<string,H51CitizenAnchor>();
        float nextSave;

        public IReadOnlyList<H51CitizenIdentity> Citizens => citizens;
        public IReadOnlyList<H51CitizenAnchor> Anchors => anchors;
        public string SavePath => Path.Combine(Application.persistentDataPath, "Hanger51", "citizens.json");

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            RefreshSceneReferences();
            Load();
            nextSave = Time.unscaledTime + autosaveSeconds;
        }

        void Update()
        {
            if (Time.unscaledTime < nextSave) return;
            nextSave = Time.unscaledTime + autosaveSeconds;
            Save();
        }

        void OnApplicationQuit() => Save();

        public void RefreshSceneReferences()
        {
            citizens.Clear();
            anchors.Clear();
            citizenById.Clear();
            anchorById.Clear();

            citizens.AddRange(GetComponentsInChildren<H51CitizenIdentity>(true));
            anchors.AddRange(GetComponentsInChildren<H51CitizenAnchor>(true));

            for (int i = 0; i < citizens.Count; i++)
            {
                H51CitizenIdentity c = citizens[i];
                if (c && !string.IsNullOrEmpty(c.CitizenId)) citizenById[c.CitizenId] = c;
            }

            for (int i = 0; i < anchors.Count; i++)
            {
                H51CitizenAnchor a = anchors[i];
                if (a && !string.IsNullOrEmpty(a.AnchorId)) anchorById[a.AnchorId] = a;
            }
        }

        public H51CitizenAnchor FindAnchor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            anchorById.TryGetValue(id, out H51CitizenAnchor result);
            return result;
        }

        public H51CitizenAnchor PickAnchor(string town, H51CitizenAnchorKind kind, int seed)
        {
            H51CitizenAnchor chosen = null;
            int seen = 0;
            System.Random rng = new System.Random(seed);
            for (int i = 0; i < anchors.Count; i++)
            {
                H51CitizenAnchor a = anchors[i];
                if (!a || a.TownName != town || a.Kind != kind) continue;
                seen++;
                if (rng.Next(seen) == 0) chosen = a;
            }
            return chosen;
        }

        public void BroadcastPlayerEvent(
            Vector3 position, float witnessRadius, string eventKey, string description,
            float valence, float severity, bool permanent = false)
        {
            float r2 = witnessRadius * witnessRadius;
            for (int i = 0; i < citizens.Count; i++)
            {
                H51CitizenIdentity citizen = citizens[i];
                if (!citizen) continue;
                Vector3 d = citizen.transform.position - position;
                d.y = 0f;
                if (d.sqrMagnitude <= r2)
                    citizen.ApplyPlayerEvent(eventKey, description, valence, severity, permanent);
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                SavePayload payload = new SavePayload();
                for (int i = 0; i < citizens.Count; i++)
                {
                    H51CitizenIdentity c = citizens[i];
                    if (!c || string.IsNullOrEmpty(c.CitizenId)) continue;
                    payload.citizens.Add(new CitizenSave
                    {
                        citizenId = c.CitizenId,
                        memories = new List<H51CitizenMemory>(c.Memories),
                        relationships = new List<H51CitizenRelationship>(c.Relationships)
                    });
                }
                File.WriteAllText(SavePath, JsonUtility.ToJson(payload, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("H51 citizen save failed: " + ex.Message, this);
            }
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return;
                SavePayload payload = JsonUtility.FromJson<SavePayload>(File.ReadAllText(SavePath));
                if (payload == null || payload.citizens == null) return;

                for (int i = 0; i < payload.citizens.Count; i++)
                {
                    CitizenSave saved = payload.citizens[i];
                    if (saved == null || string.IsNullOrEmpty(saved.citizenId)) continue;
                    if (citizenById.TryGetValue(saved.citizenId, out H51CitizenIdentity citizen))
                        citizen.RestoreSocialState(saved.memories, saved.relationships);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("H51 citizen load failed: " + ex.Message, this);
            }
        }
    }
}
