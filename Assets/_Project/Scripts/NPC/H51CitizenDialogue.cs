using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hanger51.NPCs
{
    public enum H51CitizenQuestion
    {
        Identity,
        Job,
        Home,
        CurrentActivity,
        Town,
        OpinionOfPlayer,
        PlayerMemories
    }

    [RequireComponent(typeof(H51CitizenIdentity))]
    public sealed class H51CitizenDialogue : MonoBehaviour
    {
        H51CitizenIdentity identity;
        H51CitizenBrain brain;
        string activityBeforeConversation;

        public H51CitizenIdentity Identity => identity ? identity : (identity = GetComponent<H51CitizenIdentity>());

        void Awake()
        {
            identity = GetComponent<H51CitizenIdentity>();
            brain = GetComponent<H51CitizenBrain>();
        }

        public string Greeting()
        {
            H51CitizenRelationship player = Identity.Relationship("PLAYER", false);
            if (player == null) return $"Hi. I'm {Identity.DisplayName}.";
            if (player.grudge >= 70f) return $"What do you want, {PlayerReference(player)}?";
            if (player.fear >= 65f) return "I'd rather not have trouble. What do you want?";
            if (player.trust >= 55f) return $"Hey, good to see you. I'm {Identity.DisplayName}.";
            return $"Hello. I'm {Identity.DisplayName}.";
        }

        public void CaptureActivityBeforeConversation()
        {
            if (!brain) brain = GetComponent<H51CitizenBrain>();
            activityBeforeConversation = brain && !string.IsNullOrWhiteSpace(brain.CurrentActivityDescription)
                ? brain.CurrentActivityDescription
                : "I was just going about my day.";
        }

        public void RegisterConversation()
        {
            H51CitizenRelationship player = Identity.Relationship("PLAYER");
            player.trust = Mathf.Clamp(player.trust + .35f, -100f, 100f);
            player.respect = Mathf.Clamp(player.respect + .15f, -100f, 100f);

            bool alreadyHasConversationMemory = Identity.Memories.Any(m =>
                m != null && m.eventKey == "conversation" && m.subjectId == "PLAYER");
            if (!alreadyHasConversationMemory)
                Identity.Remember("conversation", "We spoke for the first time.", "PLAYER", 2f, 4f);
        }

        public string Answer(H51CitizenQuestion question)
        {
            switch (question)
            {
                case H51CitizenQuestion.Identity:
                    return $"I'm {Identity.DisplayName}. I'm {Identity.Age}, and I live around {Identity.TownName}.";
                case H51CitizenQuestion.Job:
                    return DescribeJob();
                case H51CitizenQuestion.Home:
                    return DescribeHome();
                case H51CitizenQuestion.CurrentActivity:
                    return DescribeCurrentActivity();
                case H51CitizenQuestion.Town:
                    return DescribeTown();
                case H51CitizenQuestion.OpinionOfPlayer:
                    return DescribePlayerRelationship();
                case H51CitizenQuestion.PlayerMemories:
                    return DescribePlayerMemories();
                default:
                    return "I'm not sure what to tell you.";
            }
        }

        string DescribeJob()
        {
            H51CitizenRegistry registry = H51CitizenRegistry.Instance;
            H51CitizenAnchor anchor = registry ? registry.FindAnchor(Identity.WorkAnchorId) : null;
            string place = anchor ? anchor.DisplayLabel : null;

            if (string.IsNullOrWhiteSpace(Identity.Occupation) || Identity.Occupation == "Resident")
                return "I don't have a regular job right now.";

            if (!string.IsNullOrWhiteSpace(place))
                return $"I work as a {Identity.Occupation} at {place}.";
            return $"I work as a {Identity.Occupation} here in {Identity.TownName}.";
        }

        string DescribeHome()
        {
            H51CitizenRegistry registry = H51CitizenRegistry.Instance;
            H51CitizenAnchor anchor = registry ? registry.FindAnchor(Identity.HomeAnchorId) : null;
            if (anchor && !string.IsNullOrWhiteSpace(anchor.DisplayLabel))
                return $"I live at {anchor.DisplayLabel} in {Identity.TownName}.";
            return $"My home is here in {Identity.TownName}.";
        }

        string DescribeCurrentActivity()
        {
            if (!string.IsNullOrWhiteSpace(activityBeforeConversation))
                return "Before you stopped me, " + ToPastActivity(activityBeforeConversation);

            if (!brain) brain = GetComponent<H51CitizenBrain>();
            if (brain && !string.IsNullOrWhiteSpace(brain.CurrentActivityDescription))
                return brain.CurrentActivityDescription;

            return "I was just going about my day.";
        }

        static string ToPastActivity(string activity)
        {
            string text = activity.Trim();
            if (text.StartsWith("I'm ", StringComparison.OrdinalIgnoreCase))
                return "I was " + text.Substring(4);
            if (text.StartsWith("I am ", StringComparison.OrdinalIgnoreCase))
                return "I was " + text.Substring(5);
            return text;
        }

        string DescribeTown()
        {
            string[] variants =
            {
                $"I've got my routine here in {Identity.TownName}. Work, errands, home. It's familiar.",
                $"{Identity.TownName} is home. You get to know the roads and the people pretty quickly.",
                $"I spend most of my time around {Identity.TownName}. There's usually something that needs doing."
            };
            return variants[(Identity.AppearanceSeed & 0x7fffffff) % variants.Length];
        }

        string DescribePlayerRelationship()
        {
            H51CitizenRelationship r = Identity.Relationship("PLAYER", false);
            if (r == null) return "I don't really know you yet.";

            if (r.grudge >= 75f) return "I haven't forgotten what you've done. I don't trust you.";
            if (r.fear >= 70f) return "You make me nervous. I'd prefer you kept your distance.";
            if (r.trust >= 70f && r.respect >= 40f) return "I trust you. You've given me good reasons to.";
            if (r.trust >= 35f) return "I think you're all right. So far, anyway.";
            if (r.trust <= -45f) return "I don't trust you.";
            if (r.respect >= 45f) return "I respect you, even if I don't know you especially well.";
            return "I haven't really made up my mind about you.";
        }

        string DescribePlayerMemories()
        {
            List<H51CitizenMemory> memories = Identity.Memories
                .Where(m => m != null && m.subjectId == "PLAYER")
                .OrderByDescending(m => m.Importance)
                .ThenByDescending(m => m.createdGameMinute)
                .Take(3)
                .ToList();

            if (memories.Count == 0) return "Nothing important comes to mind. We haven't had much history.";

            string result = "What I remember most: ";
            for (int i = 0; i < memories.Count; i++)
            {
                string d = string.IsNullOrWhiteSpace(memories[i].description) ? memories[i].eventKey : memories[i].description;
                result += (i == 0 ? "" : i == memories.Count - 1 ? " And " : " ") + d;
                if (!d.EndsWith(".", StringComparison.Ordinal)) result += ".";
            }
            return result;
        }

        static string PlayerReference(H51CitizenRelationship relationship)
        {
            return relationship.fear > relationship.grudge ? "please" : "you";
        }
    }
}
