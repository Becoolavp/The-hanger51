using UnityEngine;

namespace Hanger51.Inventory
{
    public sealed class EquippedItemView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Renderer itemRenderer;

        private Material runtimeMaterial;

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
        }

        private void RefreshView()
        {
            InventoryItemDefinition equippedItem = inventory != null
                ? inventory.EquippedItem
                : null;

            if (visualRoot != null)
            {
                visualRoot.SetActive(equippedItem != null);
            }

            if (equippedItem == null || itemRenderer == null)
            {
                return;
            }

            EnsureRuntimeMaterial();

            Color equippedColor = equippedItem.PlaceholderColor;
            equippedColor.a = 1f;

            if (runtimeMaterial != null)
            {
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
