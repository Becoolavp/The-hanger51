using System;
using UnityEngine;
using UnityEngine.Events;

namespace Rng.Blood
{
    /// <summary>
    /// Something that holds blood of its own, such as an add-on's living body. A <see cref="BloodSupply"/> on the same object reads and
    /// drains it instead of keeping its own count, so every effect spends the same blood.
    /// </summary>
    public interface IBloodReservoir
    {
        /// <summary>Blood left, 0 (empty) to 1 (full).</summary>
        float BloodLeft { get; }

        /// <summary>Takes this many litres out.</summary>
        void DrainBlood(float litres);
    }

    /// <summary>
    /// The blood a body holds, optional. Smears from a <see cref="BloodSmear"/> under this object get narrower, broken and drier as the
    /// blood runs out, they spend it as they go, and the pool from <see cref="Blood.Death"/> shrinks with what is left. Without this
    /// component nothing changes.
    /// </summary>
    [AddComponentMenu("RNG/Blood Supply")]
    public class BloodSupply : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("Litres the body holds when full. An adult holds about five.")] private float _capacity = 5f;
        [SerializeField, Min(0f), Tooltip("Litres in the body now.")] private float _litres = 5f;
        [SerializeField, Min(0f), Tooltip("Litres a smear spends per metre for each metre of its width. With the defaults a body dragged with a 0.35 m smear runs dry after about 12 m.")] private float _smearCost = 1.2f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Width of a smear when the body is empty, as a share of its full width.")] private float _widthWhenEmpty = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("Strength of a smear when the body is empty: lower is thinner and streakier.")] private float _strengthWhenEmpty = 0.25f;
        [SerializeField, Range(0f, 0.9f), Tooltip("Share of smear pieces left out when the body is empty, so the trail breaks into patches. Breaks start once half the blood is gone.")] private float _gapsWhenEmpty = 0.55f;
        [SerializeField, Range(0f, 1f), Tooltip("How dry a smear looks when it is laid down by an empty body: 0 fresh and glossy, 1 as dry as old blood. Drying starts once a quarter of the blood is gone.")] private float _drynessWhenEmpty = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Size of the death pool of an empty body, as a share of a full one.")] private float _poolWhenEmpty = 0.15f;
        [SerializeField, Range(0.3f, 3f), Tooltip("How the changes follow the blood that is left: 1 evenly, above 1 the trail stays rich longer and fades at the end.")] private float _falloff = 1.6f;
        [SerializeField, Tooltip("Raised once when the body runs out of blood.")] private UnityEvent _emptied = new();

        private IBloodReservoir _reservoir;
        private bool _wasEmpty;

        /// <summary>Raised once when the body runs out of blood.</summary>
        public event Action Emptied;

        public float Capacity
        {
            get => _capacity;
            set => _capacity = Mathf.Max(0.1f, value);
        }

        /// <summary>Litres in the body now. Setting it fills or empties the body without raising <see cref="Emptied"/>.</summary>
        public float Litres
        {
            get => Reservoir != null ? Reservoir.BloodLeft * _capacity : _litres;
            set => _litres = Mathf.Clamp(value, 0f, _capacity);
        }

        /// <summary>Blood left, 0 (empty) to 1 (full).</summary>
        public float Fraction => Reservoir != null ? Mathf.Clamp01(Reservoir.BloodLeft) : Mathf.Clamp01(_litres / _capacity);

        /// <summary>The blood left after the falloff curve: what the smear and pool changes follow.</summary>
        public float Richness => Mathf.Pow(Fraction, 1f / _falloff);

        public float SmearCost
        {
            get => _smearCost;
            set => _smearCost = Mathf.Max(0f, value);
        }

        private IBloodReservoir Reservoir
        {
            get
            {
                if (_reservoir == null) _reservoir = GetComponent<IBloodReservoir>();
                return _reservoir;
            }
        }

        /// <summary>Takes blood out, for wounds, drips or your own effects.</summary>
        public void Drain(float litres)
        {
            if (litres <= 0f) return;

            if (Reservoir != null) Reservoir.DrainBlood(litres);
            else _litres = Mathf.Max(0f, _litres - litres);
            if (Fraction > 0.001f || _wasEmpty) return;

            _wasEmpty = true;
            Emptied?.Invoke();
            _emptied.Invoke();
        }

        /// <summary>Puts blood back, for a respawn or a transfusion.</summary>
        public void Refill(float litres = -1f)
        {
            _litres = litres < 0f ? _capacity : Mathf.Min(_capacity, _litres + litres);
            _wasEmpty = Fraction <= 0.001f;
        }

        /// <summary>Smear width for this much blood, as a share of the full width.</summary>
        public float WidthScale => Mathf.Lerp(_widthWhenEmpty, 1f, Richness);

        /// <summary>Smear strength for this much blood, as a share of full strength.</summary>
        public float StrengthScale => Mathf.Lerp(_strengthWhenEmpty, 1f, Richness);

        /// <summary>Chance that a piece of smear is left out.</summary>
        public float GapChance => _gapsWhenEmpty * Mathf.SmoothStep(0f, 1f, (0.5f - Fraction) / 0.45f);

        /// <summary>Dryness a new smear starts with, 0 to 1.</summary>
        public float Dryness => _drynessWhenEmpty * Mathf.SmoothStep(0f, 1f, (0.75f - Fraction) / 0.7f);

        /// <summary>Death pool size for this much blood, as a share of a full one.</summary>
        public float PoolScale => Mathf.Lerp(_poolWhenEmpty, 1f, Richness);

        private void OnValidate()
        {
            _litres = Mathf.Clamp(_litres, 0f, _capacity);
        }
    }
}
