using System;
using UnityEngine;

namespace Hanger51.NPCs
{
    [DisallowMultipleComponent]
    public sealed class H51WorldClock : MonoBehaviour
    {
        public static H51WorldClock Instance { get; private set; }

        [Header("Starting Calendar")]
        [SerializeField, Range(1,9999)] int startingYear = 2026;
        [SerializeField, Range(1,12)] int startingMonth = 6;
        [SerializeField, Range(1,31)] int startingDay = 1;
        [SerializeField, Range(0f,24f)] float startingHour = 6.5f;

        [Header("Time Progression")]
        [SerializeField, Min(.01f)] float gameMinutesPerRealSecond = 1.5f;
        [SerializeField, Min(0f)] float timeRateMultiplier = 1f;
        [SerializeField] bool paused;
        [SerializeField] double totalGameMinutes;

        int lastReportedDayIndex = -1;
        int lastReportedMinute = -1;

        public event Action<int> DayChanged;
        public event Action<int> MinuteChanged;

        public double TotalGameMinutes => totalGameMinutes;
        public int DayIndex => Mathf.FloorToInt((float)(totalGameMinutes / 1440d));
        public float MinuteOfDay => Mathf.Repeat((float)totalGameMinutes, 1440f);
        public float Hour => MinuteOfDay / 60f;
        public int Hour24 => Mathf.FloorToInt(Hour) % 24;
        public int Minute => Mathf.FloorToInt(MinuteOfDay) % 60;
        public bool Paused => paused;
        public float GameMinutesPerRealSecond => gameMinutesPerRealSecond * timeRateMultiplier;

        public DateTime StartingDate
        {
            get
            {
                int year = Mathf.Clamp(startingYear,1,9999);
                int month = Mathf.Clamp(startingMonth,1,12);
                int day = Mathf.Clamp(startingDay,1,DateTime.DaysInMonth(year,month));
                return new DateTime(year,month,day,0,0,0,DateTimeKind.Unspecified);
            }
        }

        public DateTime CurrentDateTime
        {
            get
            {
                try
                {
                    return StartingDate.AddMinutes(Math.Max(0d,totalGameMinutes));
                }
                catch (ArgumentOutOfRangeException)
                {
                    return DateTime.MaxValue;
                }
            }
        }

        public int Year => CurrentDateTime.Year;
        public int Month => CurrentDateTime.Month;
        public int Day => CurrentDateTime.Day;
        public DayOfWeek DayOfWeek => CurrentDateTime.DayOfWeek;
        public int DayOfYear => CurrentDateTime.DayOfYear;
        public string MonthName => CurrentDateTime.ToString("MMMM");
        public string DayName => CurrentDateTime.ToString("dddd");
        public bool IsWeekend => DayOfWeek == DayOfWeek.Saturday || DayOfWeek == DayOfWeek.Sunday;
        public bool IsWeekday => !IsWeekend;

        public string TimeText
        {
            get
            {
                int hour = Hour24;
                int displayHour = hour % 12;
                if (displayHour == 0) displayHour = 12;
                return displayHour.ToString("00") + ":" + Minute.ToString("00") + (hour >= 12 ? " PM" : " AM");
            }
        }

        public string DateText => DayName + ", " + MonthName + " " + Day + ", " + Year;
        public string ShortDateText => CurrentDateTime.ToString("ddd, MMM d");

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            NormalizeCalendar();

            if (totalGameMinutes <= 0d)
                totalGameMinutes = Mathf.Repeat(startingHour,24f) * 60d;

            lastReportedDayIndex = DayIndex;
            lastReportedMinute = Minute;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!paused)
            {
                totalGameMinutes +=
                    Time.deltaTime *
                    gameMinutesPerRealSecond *
                    Mathf.Max(0f,timeRateMultiplier);
            }

            int currentDay = DayIndex;
            if (currentDay != lastReportedDayIndex)
            {
                lastReportedDayIndex = currentDay;
                DayChanged?.Invoke(currentDay);
            }

            int currentMinute = Hour24 * 60 + Minute;
            if (currentMinute != lastReportedMinute)
            {
                lastReportedMinute = currentMinute;
                MinuteChanged?.Invoke(currentMinute);
            }
        }

        public void ConfigureCalendar(
            int year,
            int month,
            int day,
            float hour,
            float minutesPerRealSecond)
        {
            startingYear = Mathf.Clamp(year,1,9999);
            startingMonth = Mathf.Clamp(month,1,12);
            startingDay = Mathf.Clamp(
                day,
                1,
                DateTime.DaysInMonth(startingYear,startingMonth));
            startingHour = Mathf.Repeat(hour,24f);
            gameMinutesPerRealSecond = Mathf.Max(.01f,minutesPerRealSecond);

            if (totalGameMinutes <= 0d)
                totalGameMinutes = startingHour * 60d;
        }

        public void SetPaused(bool shouldPause)
        {
            paused = shouldPause;
        }

        public void SetTimeRateMultiplier(float multiplier)
        {
            timeRateMultiplier = Mathf.Max(0f,multiplier);
        }

        public void SetTime(int day, float hour)
        {
            totalGameMinutes =
                Mathf.Max(0,day) * 1440d +
                Mathf.Repeat(hour,24f) * 60d;
        }

        public void SetDateTime(
            int year,
            int month,
            int day,
            int hour,
            int minute)
        {
            DateTime target = new DateTime(
                Mathf.Clamp(year,1,9999),
                Mathf.Clamp(month,1,12),
                1,
                Mathf.Clamp(hour,0,23),
                Mathf.Clamp(minute,0,59),
                0,
                DateTimeKind.Unspecified);

            int maxDay = DateTime.DaysInMonth(target.Year,target.Month);
            target = new DateTime(
                target.Year,
                target.Month,
                Mathf.Clamp(day,1,maxDay),
                target.Hour,
                target.Minute,
                0,
                DateTimeKind.Unspecified);

            double minutes = (target - StartingDate).TotalMinutes;
            totalGameMinutes = Math.Max(0d,minutes);
        }

        public bool IsHourInWindow(float startHour, float endHour)
        {
            float hour = Mathf.Repeat(Hour,24f);
            startHour = Mathf.Repeat(startHour,24f);
            endHour = Mathf.Repeat(endHour,24f);

            if (Mathf.Approximately(startHour,endHour))
                return true;

            if (startHour < endHour)
                return hour >= startHour && hour < endHour;

            return hour >= startHour || hour < endHour;
        }

        void NormalizeCalendar()
        {
            startingYear = Mathf.Clamp(startingYear,1,9999);
            startingMonth = Mathf.Clamp(startingMonth,1,12);
            startingDay = Mathf.Clamp(
                startingDay,
                1,
                DateTime.DaysInMonth(startingYear,startingMonth));
            startingHour = Mathf.Repeat(startingHour,24f);
            gameMinutesPerRealSecond = Mathf.Max(.01f,gameMinutesPerRealSecond);
            timeRateMultiplier = Mathf.Max(0f,timeRateMultiplier);
            totalGameMinutes = Math.Max(0d,totalGameMinutes);
        }

        void OnValidate()
        {
            NormalizeCalendar();
        }
    }
}
