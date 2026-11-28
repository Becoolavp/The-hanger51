using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.NPCs
{
    [Serializable]
    public sealed class H51CitizenMemory
    {
        public string memoryId;
        public string eventKey;
        [TextArea] public string description;
        public string subjectId;
        [Range(-100f,100f)] public float valence;
        [Range(0f,100f)] public float severity;
        public double createdGameMinute;
        public double lastRecalledGameMinute;
        public int recallCount;
        public bool permanent;

        public float Importance => Mathf.Abs(valence) * .45f + severity * .55f + (permanent ? 100f : 0f);
    }

    [Serializable]
    public sealed class H51CitizenRelationship
    {
        public string otherId;
        [Range(-100f,100f)] public float trust;
        [Range(-100f,100f)] public float respect;
        [Range(0f,100f)] public float fear;
        [Range(-100f,100f)] public float affection;
        [Range(0f,100f)] public float grudge;
    }

    public enum H51CitizenPresentation
    {
        Unknown,
        Male,
        Female
    }

    public sealed class H51CitizenIdentity : MonoBehaviour
    {
        [Header("Persistent Identity")]
        [SerializeField] string citizenId;
        [SerializeField] string displayName;
        [SerializeField, Range(16,100)] int age = 30;
        [SerializeField] string townName;
        [SerializeField] string occupation;
        [SerializeField] string homeAnchorId;
        [SerializeField] string workAnchorId;
        [SerializeField] string visualArchetype;
        [SerializeField] H51CitizenPresentation presentation = H51CitizenPresentation.Unknown;
        [SerializeField] int appearanceSeed;

        [Header("Personality")]
        [SerializeField, Range(0f,1f)] float sociability = .5f;
        [SerializeField, Range(0f,1f)] float responsibility = .5f;
        [SerializeField, Range(0f,1f)] float temper = .5f;
        [SerializeField, Range(0f,1f)] float curiosity = .5f;

        [Header("Persistent Social State")]
        [SerializeField] List<H51CitizenMemory> memories = new List<H51CitizenMemory>();
        [SerializeField] List<H51CitizenRelationship> relationships = new List<H51CitizenRelationship>();

        public string CitizenId => citizenId;
        public string DisplayName => displayName;
        public int Age => age;
        public string TownName => townName;
        public string Occupation => occupation;
        public string HomeAnchorId => homeAnchorId;
        public string WorkAnchorId => workAnchorId;
        public string VisualArchetype => visualArchetype;
        public H51CitizenPresentation Presentation => presentation;
        public int AppearanceSeed => appearanceSeed;
        public float Sociability => sociability;
        public float Responsibility => responsibility;
        public float Temper => temper;
        public float Curiosity => curiosity;
        public List<H51CitizenMemory> Memories
        {
            get
            {
                if (memories == null) memories = new List<H51CitizenMemory>();
                return memories;
            }
        }

        public List<H51CitizenRelationship> Relationships
        {
            get
            {
                if (relationships == null) relationships = new List<H51CitizenRelationship>();
                return relationships;
            }
        }

        public void Configure(
            string id, string name, int citizenAge, string town, string job,
            string homeId, string workId, string archetype, int seed,
            float social, float responsible, float citizenTemper, float citizenCuriosity)
        {
            citizenId = id;
            displayName = name;
            age = Mathf.Clamp(citizenAge, 16, 100);
            townName = town;
            occupation = job;
            homeAnchorId = homeId;
            workAnchorId = workId;
            visualArchetype = archetype;
            appearanceSeed = seed;
            sociability = Mathf.Clamp01(social);
            responsibility = Mathf.Clamp01(responsible);
            temper = Mathf.Clamp01(citizenTemper);
            curiosity = Mathf.Clamp01(citizenCuriosity);
        }

        public void SetDisplayName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                displayName = name.Trim();
        }

        public void SetPresentation(H51CitizenPresentation value)
        {
            presentation = value;
        }

        public void SetHomeAssignment(string homeId)
        {
            homeAnchorId = homeId ?? string.Empty;
        }

        public void SetWorkAssignment(string workId, string job)
        {
            workAnchorId = workId ?? string.Empty;
            occupation = string.IsNullOrWhiteSpace(job) ? "Resident" : job;
        }

        public void SetAssignments(string homeId, string workId, string job)
        {
            SetHomeAssignment(homeId);
            SetWorkAssignment(workId, job);
        }

        public H51CitizenRelationship Relationship(string otherId, bool create = true)
        {
            if (string.IsNullOrEmpty(otherId)) return null;
            if (relationships == null) relationships = new List<H51CitizenRelationship>();
            for (int i = 0; i < relationships.Count; i++)
                if (relationships[i] != null && relationships[i].otherId == otherId)
                    return relationships[i];

            if (!create) return null;
            H51CitizenRelationship created = new H51CitizenRelationship { otherId = otherId };
            relationships.Add(created);
            return created;
        }

        public H51CitizenMemory Remember(
            string eventKey, string description, string subjectId,
            float valence, float severity, bool permanent = false)
        {
            if (memories == null) memories = new List<H51CitizenMemory>();
            double now = H51WorldClock.Instance ? H51WorldClock.Instance.TotalGameMinutes : 0d;
            string id = citizenId + "-" + eventKey + "-" + Math.Abs((description ?? "").GetHashCode()) + "-" + ((long)now).ToString();

            H51CitizenMemory memory = new H51CitizenMemory
            {
                memoryId = id,
                eventKey = eventKey,
                description = description,
                subjectId = subjectId,
                valence = Mathf.Clamp(valence, -100f, 100f),
                severity = Mathf.Clamp(severity, 0f, 100f),
                createdGameMinute = now,
                lastRecalledGameMinute = now,
                recallCount = 0,
                permanent = permanent
            };
            memories.Add(memory);
            TrimMemories(72);
            return memory;
        }

        public void ApplyPlayerEvent(string eventKey, string description, float valence, float severity, bool permanent = false)
        {
            Remember(eventKey, description, "PLAYER", valence, severity, permanent);
            H51CitizenRelationship player = Relationship("PLAYER");
            float sensitivity = Mathf.Lerp(.7f, 1.35f, temper);
            player.trust = Mathf.Clamp(player.trust + valence * .28f, -100f, 100f);
            player.respect = Mathf.Clamp(player.respect + valence * .12f, -100f, 100f);
            if (valence < 0f)
            {
                player.grudge = Mathf.Clamp(player.grudge + Mathf.Abs(valence) * .32f * sensitivity + severity * .12f, 0f, 100f);
                player.fear = Mathf.Clamp(player.fear + severity * .15f, 0f, 100f);
            }
            else
            {
                player.grudge = Mathf.Clamp(player.grudge - valence * .22f, 0f, 100f);
                player.affection = Mathf.Clamp(player.affection + valence * .10f, -100f, 100f);
            }
        }

        public void RestoreSocialState(List<H51CitizenMemory> restoredMemories, List<H51CitizenRelationship> restoredRelationships)
        {
            memories = restoredMemories ?? new List<H51CitizenMemory>();
            relationships = restoredRelationships ?? new List<H51CitizenRelationship>();
        }

        void TrimMemories(int max)
        {
            if (memories == null) memories = new List<H51CitizenMemory>();
            if (memories.Count <= max) return;
            int removableIndex = -1;
            float lowest = float.MaxValue;
            for (int i = 0; i < memories.Count; i++)
            {
                H51CitizenMemory m = memories[i];
                if (m == null || m.permanent) continue;
                if (m.Importance < lowest)
                {
                    lowest = m.Importance;
                    removableIndex = i;
                }
            }
            if (removableIndex >= 0) memories.RemoveAt(removableIndex);
        }
    }
}
