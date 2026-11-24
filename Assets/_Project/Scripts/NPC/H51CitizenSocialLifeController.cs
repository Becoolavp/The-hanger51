using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51CitizenSocialLifeController : MonoBehaviour
    {
        [SerializeField, Min(1f)] float realSecondsBetweenSocialChecks = 6f;
        [SerializeField, Min(1f)] float socialRadius = 10f;
        [SerializeField, Min(5f)] float repeatCooldownGameMinutes = 90f;

        H51CitizenRegistry registry;
        float nextCheck;
        readonly Dictionary<string,double> lastPairSocialMinute = new Dictionary<string,double>();

        void Awake()
        {
            registry = GetComponent<H51CitizenRegistry>();
            if (!registry) registry = GetComponentInParent<H51CitizenRegistry>();
        }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + realSecondsBetweenSocialChecks;

            if (!registry) registry = H51CitizenRegistry.Instance;
            if (!registry) return;

            IReadOnlyList<H51CitizenIdentity> citizens = registry.Citizens;
            if (citizens == null || citizens.Count < 2) return;

            double now = H51WorldClock.Instance ? H51WorldClock.Instance.TotalGameMinutes : 0d;
            int processed = 0;

            for (int i = 0; i < citizens.Count && processed < 5; i++)
            {
                H51CitizenIdentity a = citizens[i];
                if (!a || !a.gameObject.activeInHierarchy) continue;

                H51CitizenIdentity b = FindNearbyPartner(a, citizens);
                if (!b) continue;

                string key = PairKey(a.CitizenId, b.CitizenId);
                if (lastPairSocialMinute.TryGetValue(key, out double last) &&
                    now > 0d &&
                    now - last < repeatCooldownGameMinutes)
                    continue;

                Socialize(a, b);
                lastPairSocialMinute[key] = now;
                processed++;
            }
        }

        H51CitizenIdentity FindNearbyPartner(
            H51CitizenIdentity citizen,
            IReadOnlyList<H51CitizenIdentity> citizens)
        {
            float best = socialRadius;
            H51CitizenIdentity result = null;

            for (int i = 0; i < citizens.Count; i++)
            {
                H51CitizenIdentity other = citizens[i];
                if (!other || other == citizen || !other.gameObject.activeInHierarchy) continue;
                if (!string.Equals(other.TownName, citizen.TownName, StringComparison.Ordinal)) continue;

                float d = Planar(citizen.transform.position, other.transform.position);
                if (d >= best) continue;

                best = d;
                result = other;
            }

            return result;
        }

        void Socialize(H51CitizenIdentity a, H51CitizenIdentity b)
        {
            H51CitizenRelationship ab = a.Relationship(b.CitizenId);
            H51CitizenRelationship ba = b.Relationship(a.CitizenId);

            float compatibility =
                .35f +
                (a.Sociability + b.Sociability) * .20f +
                (1f - Mathf.Abs(a.Temper - b.Temper)) * .15f;

            float trustGain = Mathf.Lerp(.25f, 1.15f, compatibility);
            float affectionGain = Mathf.Lerp(.10f, .70f, compatibility);

            ab.trust = Mathf.Clamp(ab.trust + trustGain, -100f, 100f);
            ba.trust = Mathf.Clamp(ba.trust + trustGain, -100f, 100f);
            ab.affection = Mathf.Clamp(ab.affection + affectionGain, -100f, 100f);
            ba.affection = Mathf.Clamp(ba.affection + affectionGain, -100f, 100f);

            TrySharePlayerMemory(a, b);
            TrySharePlayerMemory(b, a);
        }

        void TrySharePlayerMemory(H51CitizenIdentity source, H51CitizenIdentity listener)
        {
            H51CitizenMemory memory = source.Memories
                .Where(m =>
                    m != null &&
                    m.subjectId == "PLAYER" &&
                    !string.IsNullOrWhiteSpace(m.eventKey) &&
                    !m.eventKey.StartsWith("gossip_", StringComparison.Ordinal) &&
                    m.Importance >= 5f)
                .OrderByDescending(m => m.Importance)
                .FirstOrDefault();

            if (memory == null) return;

            string gossipKey = "gossip_" + source.CitizenId + "_" + memory.eventKey;
            bool alreadyKnows = listener.Memories.Any(m =>
                m != null &&
                m.subjectId == "PLAYER" &&
                m.eventKey == gossipKey);

            if (alreadyKnows) return;

            string description =
                source.DisplayName + " told me: " +
                FirstPersonToThirdPerson(memory.description, source.DisplayName);

            float rumorValence = Mathf.Clamp(memory.valence * .45f, -15f, 15f);
            float rumorSeverity = Mathf.Clamp(memory.severity * .55f, 1f, 35f);

            listener.Remember(
                gossipKey,
                description,
                "PLAYER",
                rumorValence,
                rumorSeverity,
                false);

            H51CitizenRelationship player = listener.Relationship("PLAYER");
            player.trust = Mathf.Clamp(player.trust + rumorValence * .10f, -100f, 100f);
            player.respect = Mathf.Clamp(player.respect + rumorValence * .04f, -100f, 100f);

            if (rumorValence < 0f)
                player.grudge = Mathf.Clamp(player.grudge + Mathf.Abs(rumorValence) * .08f, 0f, 100f);
            else
                player.affection = Mathf.Clamp(player.affection + rumorValence * .04f, -100f, 100f);
        }

        static string FirstPersonToThirdPerson(string text, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(text))
                return sourceName + " had something to say about you.";

            string result = text.Trim();
            result = result.Replace("You ", "The player ");
            result = result.Replace("you ", "the player ");
            result = result.Replace("your ", "the player's ");
            return result;
        }

        static string PairKey(string a, string b)
        {
            return string.CompareOrdinal(a, b) <= 0
                ? a + "|" + b
                : b + "|" + a;
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x*x + z*z);
        }
    }
}
