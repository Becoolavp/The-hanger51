using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51GunTestTarget : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximumHealth = 600f;
        [SerializeField] private Transform plateRoot;
        [SerializeField] private Renderer plateRenderer;
        [SerializeField] private TextMesh statusText;
        [SerializeField, Min(0.5f)] private float resetDelaySeconds = 4f;

        private float currentHealth;
        private int hitCount;
        private bool destroyed;
        private float resetAt;
        private float flashUntil;
        private Quaternion normalRotation;
        private Vector3 normalScale;
        private MaterialPropertyBlock propertyBlock;
        private Color normalColor = new Color(0.42f, 0.45f, 0.48f, 1f);

        public float CurrentHealth => currentHealth;
        public float MaximumHealth => maximumHealth;
        public int HitCount => hitCount;
        public bool Destroyed => destroyed;

        public void Configure(Transform configuredPlate, Renderer configuredRenderer, TextMesh configuredText, float health)
        {
            plateRoot = configuredPlate;
            plateRenderer = configuredRenderer;
            statusText = configuredText;
            maximumHealth = Mathf.Max(1f, health);
            CapturePose();
            ResetTarget();
        }

        private void Awake()
        {
            CapturePose();
            if (currentHealth <= 0f) currentHealth = maximumHealth;
            UpdateStatusText();
        }

        private void OnEnable()
        {
            CapturePose();
            if (currentHealth <= 0f) currentHealth = maximumHealth;
            UpdateStatusText();
        }

        private void Update()
        {
            if (destroyed && Time.time >= resetAt)
            {
                ResetTarget();
                return;
            }

            if (plateRoot != null)
            {
                Quaternion desired = destroyed
                    ? normalRotation * Quaternion.Euler(72f, 0f, 0f)
                    : normalRotation;
                plateRoot.localRotation = Quaternion.Slerp(
                    plateRoot.localRotation,
                    desired,
                    1f - Mathf.Exp(-7f * Time.deltaTime));

                float pulse = !destroyed && Time.time < flashUntil ? 1.035f : 1f;
                plateRoot.localScale = Vector3.Lerp(
                    plateRoot.localScale,
                    normalScale * pulse,
                    1f - Mathf.Exp(-18f * Time.deltaTime));
            }

            RefreshPlateColor();
        }

        public void RegisterHit(Vector3 hitPoint, Vector3 shotDirection, float damage)
        {
            if (destroyed) return;

            currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(1f, damage));
            hitCount++;
            flashUntil = Time.time + 0.10f;

            if (plateRoot != null)
            {
                Vector3 localDirection = transform.InverseTransformDirection(shotDirection.normalized);
                float yawKick = Mathf.Clamp(localDirection.x * 2.5f, -2.5f, 2.5f);
                plateRoot.localRotation *= Quaternion.Euler(-1.2f, yawKick, 0f);
            }

            if (currentHealth <= 0f)
            {
                destroyed = true;
                resetAt = Time.time + Mathf.Max(0.5f, resetDelaySeconds);
            }

            UpdateStatusText();
        }

        public void ResetTarget()
        {
            CapturePose();
            currentHealth = maximumHealth;
            hitCount = 0;
            destroyed = false;
            resetAt = 0f;
            flashUntil = 0f;
            if (plateRoot != null)
            {
                plateRoot.localRotation = normalRotation;
                plateRoot.localScale = normalScale;
            }
            RefreshPlateColor(true);
            UpdateStatusText();
        }

        private void CapturePose()
        {
            if (plateRoot != null && normalScale == Vector3.zero)
            {
                normalRotation = plateRoot.localRotation;
                normalScale = plateRoot.localScale;
            }

            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            if (plateRenderer != null && plateRenderer.sharedMaterial != null)
            {
                Material material = plateRenderer.sharedMaterial;
                if (material.HasProperty("_BaseColor")) normalColor = material.GetColor("_BaseColor");
                else if (material.HasProperty("_Color")) normalColor = material.GetColor("_Color");
            }
        }

        private void RefreshPlateColor(bool force = false)
        {
            if (plateRenderer == null) return;

            Color color = destroyed
                ? new Color(0.32f, 0.055f, 0.035f, 1f)
                : Time.time < flashUntil
                    ? new Color(1f, 0.70f, 0.18f, 1f)
                    : normalColor;

            propertyBlock ??= new MaterialPropertyBlock();
            plateRenderer.GetPropertyBlock(propertyBlock);
            Material material = plateRenderer.sharedMaterial;
            if (material != null && material.HasProperty("_BaseColor"))
            {
                propertyBlock.SetColor("_BaseColor", color);
            }
            if (material != null && material.HasProperty("_Color"))
            {
                propertyBlock.SetColor("_Color", color);
            }
            if (material != null && material.HasProperty("_EmissionColor"))
            {
                propertyBlock.SetColor(
                    "_EmissionColor",
                    Time.time < flashUntil ? color * 1.8f : Color.black);
            }
            plateRenderer.SetPropertyBlock(propertyBlock);
        }

        private void UpdateStatusText()
        {
            if (statusText == null) return;
            statusText.text = destroyed
                ? $"TARGET DESTROYED\nHits: {hitCount}\nResetting..."
                : $"GUN TEST TARGET\n{currentHealth:F0} / {maximumHealth:F0} HP\nHits: {hitCount}";
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
            resetDelaySeconds = Mathf.Max(0.5f, resetDelaySeconds);
        }
    }
}
