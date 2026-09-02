using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51NitrogenCartInteriorParkingSetup
    {
        private const string HangarRootName = "Hanger 51 Test Hangar";
        private const string CartName = "P-51 Nitrogen Tire Service Cart";

        // This is the interior service-bay position that Step 30 already intended
        // to use before its world-space ground ray accidentally snapped the cart
        // upward onto the hangar roof.
        private static readonly Vector3 InteriorLocalPosition =
            new Vector3(-7.1f, 0.02f, 5.8f);
        private static readonly Vector3 InteriorLocalEuler =
            new Vector3(0f, 25f, 0f);

        [MenuItem("Hanger 51/P-51 Mustang/32 - Park Nitrogen Cart Inside Hangar")]
        public static void ParkNitrogenCartInsideHangar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 32 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject hangar = GameObject.Find(HangarRootName);
            P51NitrogenCartController cart = Object.FindFirstObjectByType<P51NitrogenCartController>(
                FindObjectsInactive.Include);

            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 32 failed. Open the saved movement-test scene first.");
                return;
            }
            if (hangar == null)
            {
                Debug.LogError($"P-51 Step 32 failed. Could not find '{HangarRootName}'.");
                return;
            }
            if (cart == null)
            {
                Debug.LogError("P-51 Step 32 failed. The nitrogen cart is missing. Run P-51 Step 30 first.");
                return;
            }

            Undo.RecordObject(cart.transform, "Park nitrogen cart inside hangar");
            cart.StopMoving();
            cart.Disconnect();

            cart.transform.SetParent(hangar.transform, false);
            cart.transform.localPosition = InteriorLocalPosition;
            cart.transform.localRotation = Quaternion.Euler(InteriorLocalEuler);

            Rigidbody body = cart.GetComponent<Rigidbody>();
            if (body != null)
            {
                Undo.RecordObject(body, "Reset nitrogen cart movement");
                body.position = cart.transform.position;
                body.rotation = cart.transform.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                EditorUtility.SetDirty(body);
            }

            EditorUtility.SetDirty(cart);
            EditorUtility.SetDirty(cart.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 32 moved the nitrogen cart but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 32 parked the cart, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = cart.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log(
                $"P-51 Step 32 complete. The nitrogen cart is now parked on the hangar floor at local position {InteriorLocalPosition}; the roof-catching ground ray is bypassed for this repair.",
                cart);
        }

        [MenuItem("Hanger 51/P-51 Mustang/33 - Validate Nitrogen Cart Interior Parking")]
        public static void ValidateNitrogenCartInteriorParking()
        {
            GameObject hangar = GameObject.Find(HangarRootName);
            P51NitrogenCartController cart = Object.FindFirstObjectByType<P51NitrogenCartController>(
                FindObjectsInactive.Include);

            if (hangar == null || cart == null)
            {
                Debug.LogError("P-51 Step 33 failed. Hangar or nitrogen cart is missing.");
                return;
            }

            bool parentCorrect = cart.transform.IsChildOf(hangar.transform);
            Vector3 local = cart.transform.localPosition;
            bool nearInteriorSpot = Vector3.Distance(local, InteriorLocalPosition) <= 0.25f;
            bool floorHeight = local.y > -0.25f && local.y < 0.75f;

            if (!parentCorrect || !nearInteriorSpot || !floorHeight)
            {
                Debug.LogError(
                    $"P-51 Step 33 failed. Cart parent/position is not the intended interior floor parking spot. Parent OK: {parentCorrect}, local position: {local}.",
                    cart);
                return;
            }

            Debug.Log(
                $"P-51 Step 33 passed. Nitrogen cart is parented to the hangar and parked at floor-level local position {local}, not on the roof.",
                cart);
        }
    }
}
