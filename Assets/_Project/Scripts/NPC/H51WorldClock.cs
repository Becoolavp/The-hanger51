using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51WorldClock : MonoBehaviour
    {
        public static H51WorldClock Instance { get; private set; }

        [SerializeField, Range(0f,24f)] float startingHour = 6.5f;
        [SerializeField, Min(.01f)] float gameMinutesPerRealSecond = 1.5f;
        [SerializeField] double totalGameMinutes;

        public double TotalGameMinutes => totalGameMinutes;
        public int DayIndex => Mathf.FloorToInt((float)(totalGameMinutes / 1440d));
        public float MinuteOfDay => Mathf.Repeat((float)totalGameMinutes, 1440f);
        public float Hour => MinuteOfDay / 60f;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (totalGameMinutes <= 0d) totalGameMinutes = startingHour * 60d;
        }

        void Update()
        {
            totalGameMinutes += Time.deltaTime * gameMinutesPerRealSecond;
        }

        public void SetTime(int day, float hour)
        {
            totalGameMinutes = Mathf.Max(0, day) * 1440d + Mathf.Repeat(hour, 24f) * 60d;
        }
    }
}
