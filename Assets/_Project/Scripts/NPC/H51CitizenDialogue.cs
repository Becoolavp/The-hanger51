using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hanger51.NPCs
{
    public enum H51CitizenSocialAction
    {
        FriendlyChat,
        Compliment,
        OfferHelp,
        Apologize
    }

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
            double now = H51WorldClock.Instance ? H51WorldClock.Instance.TotalGameMinutes : 0d;

            H51CitizenMemory latestConversation = Identity.Memories
                .Where(m => m != null && m.eventKey == "conversation" && m.subjectId == "PLAYER")
                .OrderByDescending(m => m.createdGameMinute)
                .FirstOrDefault();

            bool firstConversation = latestConversation == null;
            bool enoughTimePassed =
                firstConversation ||
                now <= 0d ||
                now - latestConversation.createdGameMinute >= 90d;

            if (!enoughTimePassed) return;

            player.trust = Mathf.Clamp(player.trust + (firstConversation ? .75f : .25f), -100f, 100f);
            player.respect = Mathf.Clamp(player.respect + (firstConversation ? .30f : .10f), -100f, 100f);

            Identity.Remember(
                "conversation",
                firstConversation ? "We spoke for the first time." : "We had another conversation.",
                "PLAYER",
                firstConversation ? 2f : 1f,
                firstConversation ? 4f : 2f);
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

        public string RelationshipStatus()
        {
            H51CitizenRelationship r = Identity.Relationship("PLAYER", false);
            if (r == null) return "Stranger";
            if (r.grudge >= 70f || r.trust <= -55f) return "Hostile";
            if (r.fear >= 65f) return "Afraid";
            if (r.trust >= 70f && r.affection >= 35f) return "Close friend";
            if (r.trust >= 50f) return "Trusted";
            if (r.trust >= 25f) return "Friendly";
            if (r.trust <= -20f) return "Wary";
            return "Acquaintance";
        }

        public string PerformSocialAction(H51CitizenSocialAction action)
        {
            H51CitizenRelationship relationship = Identity.Relationship("PLAYER");
            double now = H51WorldClock.Instance ? H51WorldClock.Instance.TotalGameMinutes : 0d;

            string eventKey;
            string memory;
            float valence;
            float severity;
            double cooldownMinutes;

            switch (action)
            {
                case H51CitizenSocialAction.FriendlyChat:
                    eventKey = "friendly_chat";
                    memory = "We took some time to talk and get to know each other.";
                    valence = Mathf.Lerp(2.2f, 4.2f, Identity.Sociability);
                    severity = 4f;
                    cooldownMinutes = 180d;
                    break;

                case H51CitizenSocialAction.Compliment:
                    eventKey = "compliment";
                    memory = "You gave me a sincere compliment.";
                    valence = relationship.grudge > 45f
                        ? .8f
                        : Mathf.Lerp(1.8f, 3.8f, Identity.Sociability);
                    severity = 3f;
                    cooldownMinutes = 360d;
                    break;

                case H51CitizenSocialAction.OfferHelp:
                    eventKey = "offered_help";
                    memory = "You offered to lend a hand if I needed it.";
                    valence = relationship.grudge > 60f ? 1f : 4.4f;
                    severity = 6f;
                    cooldownMinutes = 720d;
                    break;

                case H51CitizenSocialAction.Apologize:
                    if (relationship.grudge < 5f && relationship.trust > -10f)
                        return "I don't think you owe me an apology.";

                    eventKey = "apology";
                    memory = "You apologized for how things had gone between us.";
                    valence = 3.5f;
                    severity = 5f;
                    cooldownMinutes = 360d;
                    break;

                default:
                    return "I'm not sure how to respond to that.";
            }

            H51CitizenMemory previous = Identity.Memories
                .Where(m => m != null && m.subjectId == "PLAYER" && m.eventKey == eventKey)
                .OrderByDescending(m => m.createdGameMinute)
                .FirstOrDefault();

            if (previous != null &&
                now > 0d &&
                now - previous.createdGameMinute < cooldownMinutes)
            {
                switch (action)
                {
                    case H51CitizenSocialAction.FriendlyChat:
                        return "We've already had a pretty good talk recently. Catch me again later.";
                    case H51CitizenSocialAction.Compliment:
                        return "That's kind of you, but you don't have to keep saying it.";
                    case H51CitizenSocialAction.OfferHelp:
                        return "I remember your offer. If I need a hand, I'll keep it in mind.";
                    case H51CitizenSocialAction.Apologize:
                        return "I heard your apology. Give me a little time.";
                }
            }

            Identity.ApplyPlayerEvent(eventKey, memory, valence, severity);

            if (action == H51CitizenSocialAction.Apologize)
            {
                relationship.grudge = Mathf.Max(
                    0f,
                    relationship.grudge - Mathf.Lerp(8f, 17f, 1f - Identity.Temper));
                relationship.fear = Mathf.Max(0f, relationship.fear - 4f);
                relationship.trust = Mathf.Clamp(relationship.trust + 2.5f, -100f, 100f);

                return relationship.grudge > 45f
                    ? "I appreciate you saying it. I'm still not ready to forget what happened."
                    : "Thank you. That does mean something to me.";
            }

            if (action == H51CitizenSocialAction.FriendlyChat)
            {
                return Identity.Sociability > .55f
                    ? "Yeah, I enjoyed that. It's nice actually getting to know somebody around here."
                    : "That was nice. I'm usually a little quieter, but I don't mind talking with you.";
            }

            if (action == H51CitizenSocialAction.Compliment)
            {
                return relationship.grudge > 45f
                    ? "Thanks. I'm still not sure where we stand, though."
                    : "Thank you. That's genuinely nice of you to say.";
            }

            return relationship.grudge > 60f
                ? "I'll remember that you offered. We'll see."
                : "I appreciate that. If something comes up, I'll remember you offered to help.";
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
