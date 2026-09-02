using UnityEngine;
using UnityEngine.Rendering;

namespace Hanger51.Inventory
{
    public sealed class EquippedItemView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Renderer itemRenderer;

        private Material runtimeMaterial;
        private InventoryItemDefinition displayedItem;
        private GameObject displayedPrefabModel;
        private string displayedConditionSignature = string.Empty;

        private void Awake()
        {
            if (inventory == null)
            {
                inventory = FindFirstObjectByType<PlayerInventory>();
            }

            EnsureRuntimeMaterial();
            RefreshView();
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged += RefreshView;
            }

            RefreshView();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= RefreshView;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }

            if (displayedPrefabModel != null)
            {
                Destroy(displayedPrefabModel);
            }
        }

        private void RefreshView()
        {
            InventoryItemDefinition equippedItem = inventory != null
                ? inventory.EquippedItem
                : null;
            EnginePartConditionData equippedCondition = inventory != null
                ? inventory.EquippedCondition
                : null;
            string conditionSignature = equippedCondition != null
                ? equippedCondition.Signature
                : string.Empty;

            if (equippedItem == displayedItem
                && conditionSignature == displayedConditionSignature)
            {
                SetCurrentVisibility(equippedItem != null);
                return;
            }

            displayedItem = equippedItem;
            displayedConditionSignature = conditionSignature;
            DestroyDisplayedPrefabModel();

            if (equippedItem == null)
            {
                SetCurrentVisibility(false);
                return;
            }

            if (equippedItem.WorldPrefab != null)
            {
                CreatePrefabView(equippedItem.WorldPrefab, equippedCondition);
                if (visualRoot != null)
                {
                    visualRoot.SetActive(false);
                }

                return;
            }

            SetCurrentVisibility(true);
            ApplyFallbackColor(equippedItem, equippedCondition);
        }

        private void CreatePrefabView(
            GameObject prefab,
            EnginePartConditionData equippedCondition)
        {
            displayedPrefabModel = Instantiate(prefab, transform);
            displayedPrefabModel.name = $"Equipped {displayedItem.DisplayName} Model";
            displayedPrefabModel.transform.localPosition = new Vector3(0.43f, -0.34f, 0.82f);
            displayedPrefabModel.transform.localRotation = Quaternion.Euler(12f, -18f, 8f);
            displayedPrefabModel.SetActive(true);

            Collider[] colliders = displayedPrefabModel.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = false;
            }

            InventoryPickup[] pickups =
                displayedPrefabModel.GetComponentsInChildren<InventoryPickup>(true);
            for (int index = 0; index < pickups.Length; index++)
            {
                pickups[index].enabled = false;
            }

            Renderer[] renderers = displayedPrefabModel.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].shadowCastingMode = ShadowCastingMode.Off;
                renderers[index].receiveShadows = false;
            }

            ScalePrefabForFirstPersonView(renderers);

            EnginePartConditionVisual conditionVisual =
                displayedPrefabModel.GetComponent<EnginePartConditionVisual>();
            if (conditionVisual == null && equippedCondition != null)
            {
                conditionVisual = displayedPrefabModel.AddComponent<EnginePartConditionVisual>();
            }
            conditionVisual?.Configure(equippedCondition);
        }

        private void ScalePrefabForFirstPersonView(Renderer[] renderers)
        {
            if (displayedPrefabModel == null || renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largestDimension <= 0.0001f)
            {
                return;
            }

            float scale = 0.42f / largestDimension;
            displayedPrefabModel.transform.localScale = Vector3.one * scale;
        }

        private void ApplyFallbackColor(
            InventoryItemDefinition equippedItem,
            EnginePartConditionData equippedCondition)
        {
            if (itemRenderer == null)
            {
                return;
            }

            EnsureRuntimeMaterial();

            Color equippedColor = equippedItem.PlaceholderColor;
            equippedColor.a = 1f;
            if (equippedCondition != null)
            {
                Color damaged = equippedCondition.Kind == EnginePartConditionKind.SparkPlug
                    ? new Color(0.12f, 0.075f, 0.035f, 1f)
                    : new Color(0.16f, 0.09f, 0.05f, 1f);
                equippedColor = Color.Lerp(
                    damaged,
                    equippedColor,
                    equippedCondition.Health / 100f);
            }

            if (runtimeMaterial == null)
            {
                return;
            }

            runtimeMaterial.color = equippedColor;

            if (runtimeMaterial.HasProperty("_BaseColor"))
            {
                runtimeMaterial.SetColor("_BaseColor", equippedColor);
            }

            if (runtimeMaterial.HasProperty("_Color"))
            {
                runtimeMaterial.SetColor("_Color", equippedColor);
            }
        }

        private void SetCurrentVisibility(bool visible)
        {
            if (displayedPrefabModel != null)
            {
                displayedPrefabModel.SetActive(visible);
            }

            if (visualRoot != null)
            {
                visualRoot.SetActive(visible && displayedPrefabModel == null);
            }
        }

        private void DestroyDisplayedPrefabModel()
        {
            if (displayedPrefabModel == null)
            {
                return;
            }

            Destroy(displayedPrefabModel);
            displayedPrefabModel = null;
        }

        private void EnsureRuntimeMaterial()
        {
            if (runtimeMaterial != null || itemRenderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return;
            }

            runtimeMaterial = new Material(shader)
            {
                name = "Equipped Item Runtime Material"
            };

            itemRenderer.material = runtimeMaterial;
        }
    }
}
