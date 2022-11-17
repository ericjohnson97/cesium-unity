using System;
using UnityEditor;
using UnityEngine;

namespace CesiumForUnity
{
    [InitializeOnLoad]
    public static class CesiumEditorUtility
    {
        public static class InspectorGUI
        {
            public static void ClampedIntField(
                SerializedProperty property, int min, int max, GUIContent label)
            {
                if (property.propertyType == SerializedPropertyType.Integer)
                {
                    int value = EditorGUILayout.IntField(label, property.intValue);
                    property.intValue = Math.Clamp(value, min, max);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        label.text, "Use ClampedIntField for int only.");
                }
            }

            public static void ClampedFloatField(
                SerializedProperty property, float min, float max, GUIContent label)
            {
                if (property.propertyType == SerializedPropertyType.Float)
                {
                    float value = EditorGUILayout.FloatField(label, property.floatValue);
                    property.floatValue = Math.Clamp(value, min, max);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        label.text, "Use ClampedFloatField for float only.");
                }
            }

            public static void ClampedDoubleField(
                SerializedProperty property, double min, double max, GUIContent label)
            {
                // SerializedPropertyType.Float is used for both float and double;
                // SerializedPropertyType.Double does not exist.
                if (property.propertyType == SerializedPropertyType.Float)
                {
                    double value = EditorGUILayout.DoubleField(label, property.doubleValue);
                    property.doubleValue = Math.Clamp(value, min, max);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        label.text, "Use ClampedDoubleField for double only.");
                }
            }
        }

        static CesiumEditorUtility()
        {
            EditorApplication.update += CheckProjectFilesForTextMeshPro;

            Cesium3DTileset.OnCesium3DTilesetLoadFailure +=
                HandleCesium3DTilesetLoadFailure;
            CesiumRasterOverlay.OnCesiumRasterOverlayLoadFailure +=
                HandleCesiumRasterOverlayLoadFailure;
        }

        static void CheckProjectFilesForTextMeshPro()
        {
            UnityEngine.Object tmpSettings = Resources.Load("TMP Settings");
            if (tmpSettings != null)
            {
                return;
            }

            TextMeshProPromptWindow.ShowWindow();

            EditorApplication.update -= CheckProjectFilesForTextMeshPro;
        }

        static void
        HandleCesium3DTilesetLoadFailure(Cesium3DTilesetLoadFailureDetails details)
        {
            if (details.tileset == null)
            {
                return;
            }

            // Don't open a troubleshooting panel during play mode.
            if (EditorApplication.isPlaying)
            {
                return;
            }

            // Check for a 401 connecting to Cesium ion, which means the token is invalid
            // (or perhaps the asset ID is). Also check for a 404, because ion returns 404
            // when the token is valid but not authorized for the asset.
            if (details.type == Cesium3DTilesetLoadType.CesiumIon
                && (details.httpStatusCode == 401 || details.httpStatusCode == 404))
            {
                IonTokenTroubleshootingWindow.ShowWindow(details.tileset, true);
            }

            Debug.Log(details.message);
        }

        static void
        HandleCesiumRasterOverlayLoadFailure(CesiumRasterOverlayLoadFailureDetails details)
        {
            if (details.overlay == null)
            {
                return;
            }

            // Don't open a troubleshooting panel during play mode.
            if (EditorApplication.isPlaying)
            {
                return;
            }

            // Check for a 401 connecting to Cesium ion, which means the token is invalid
            // (or perhaps the asset ID is). Also check for a 404, because ion returns 404
            // when the token is valid but not authorized for the asset.
            if (details.type == CesiumRasterOverlayLoadType.CesiumIon
                && (details.httpStatusCode == 401 || details.httpStatusCode == 404))
            {
                IonTokenTroubleshootingWindow.ShowWindow(details.overlay, true);
            }

            Debug.Log(details.message);
        }

        public static Cesium3DTileset? FindFirstTileset()
        {
            Cesium3DTileset[] tilesets =
                UnityEngine.Object.FindObjectsOfType<Cesium3DTileset>(true);
            for (int i = 0; i < tilesets.Length; i++)
            {
                Cesium3DTileset tileset = tilesets[i];
                if (tileset != null)
                {
                    return tileset;
                }
            }

            return null;
        }

        public static Cesium3DTileset? FindFirstTilesetWithAssetID(long assetID)
        {
            Cesium3DTileset[] tilesets =
                UnityEngine.Object.FindObjectsOfType<Cesium3DTileset>(true);
            for (int i = 0; i < tilesets.Length; i++)
            {
                Cesium3DTileset tileset = tilesets[i];
                if (tileset != null && tileset.ionAssetID == assetID)
                {
                    return tileset;
                }
            }

            return null;
        }

        public static CesiumGeoreference? FindFirstGeoreference()
        {
            CesiumGeoreference[] georeferences =
               UnityEngine.Object.FindObjectsOfType<CesiumGeoreference>(true);
            for (int i = 0; i < georeferences.Length; i++)
            {
                CesiumGeoreference georeference = georeferences[i];
                if (georeference != null)
                {
                    return georeference;
                }
            }

            return null;
        }

        public static Cesium3DTileset CreateTileset(string name, long assetID)
        {
            // Find a georeference in the scene, or create one if none exists.
            CesiumGeoreference? georeference = CesiumEditorUtility.FindFirstGeoreference();
            if (georeference == null)
            {
                GameObject georeferenceGameObject =
                    new GameObject("CesiumGeoreference");
                georeference =
                    georeferenceGameObject.AddComponent<CesiumGeoreference>();
                Undo.RegisterCreatedObjectUndo(georeferenceGameObject, "Create Georeference");
            }

            GameObject tilesetGameObject = new GameObject(name);
            tilesetGameObject.transform.SetParent(georeference.gameObject.transform);

            Cesium3DTileset tileset = tilesetGameObject.AddComponent<Cesium3DTileset>();
            tileset.name = name;
            tileset.ionAssetID = assetID;

            Undo.RegisterCreatedObjectUndo(tilesetGameObject, "Create Tileset");

            return tileset;
        }

        public static CesiumIonRasterOverlay
            AddBaseOverlayToTileset(Cesium3DTileset tileset, long assetID)
        {
            GameObject gameObject = tileset.gameObject;
            CesiumRasterOverlay overlay = gameObject.GetComponent<CesiumRasterOverlay>();
            if (overlay != null)
            {
                Undo.DestroyObjectImmediate(overlay);
            }

            CesiumIonRasterOverlay ionOverlay = Undo.AddComponent<CesiumIonRasterOverlay>(gameObject);
            ionOverlay.ionAssetID = assetID;

            return ionOverlay;
        }

        private static CesiumVector3
        TransformCameraPositionToEarthCenteredEarthFixed(CesiumGeoreference georeference)
        {
            Camera camera = SceneView.lastActiveSceneView.camera;
            Vector3 position = camera.transform.position;
            CesiumVector3 positionUnity = new CesiumVector3()
            {
                x = position.x,
                y = position.y,
                z = position.z
            };

            return georeference.TransformUnityWorldPositionToEarthCenteredEarthFixed(
                positionUnity);
        }

        private static void SetSceneViewPositionRotation(
            Vector3 position,
            Quaternion rotation)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            sceneView.pivot =
                position + sceneView.camera.transform.forward * sceneView.cameraDistance;
            SceneView.lastActiveSceneView.Repaint();
        }

        public static void PlaceGeoreferenceAtCameraPosition(CesiumGeoreference georeference)
        {
            Undo.RecordObject(georeference, "Place Georeference Origin at Camera Position");

            // Disable all sub-scenes before repositioning the georeference.
            CesiumSubScene[] subScenes =
                georeference.gameObject.GetComponentsInChildren<CesiumSubScene>();
            for (int i = 0; i < subScenes.Length; i++)
            {
                subScenes[i].gameObject.SetActive(false);
            }

            CesiumVector3 positionECEF =
                CesiumEditorUtility.TransformCameraPositionToEarthCenteredEarthFixed(georeference);
            georeference.SetOriginEarthCenteredEarthFixed(
                positionECEF.x,
                positionECEF.y,
                positionECEF.z);

            // Teleport the camera back to the georeference's position so it stays
            // at the middle of the subscene.
            // TODO: This will have to change when we factor in Unity transforms.
            CesiumEditorUtility.SetSceneViewPositionRotation(
                Vector3.zero, SceneView.lastActiveSceneView.rotation);
        }

        public static CesiumSubScene CreateSubScene(CesiumGeoreference georeference)
        {
            CesiumEditorUtility.PlaceGeoreferenceAtCameraPosition(georeference);
            CesiumVector3 positionECEF = new CesiumVector3()
            {
                x = georeference.ecefX,
                y = georeference.ecefY,
                z = georeference.ecefZ
            };
            
            GameObject subSceneGameObject = new GameObject();
            subSceneGameObject.transform.parent = georeference.transform;
            Undo.RegisterCreatedObjectUndo(subSceneGameObject, "Create Sub-Scene");

            CesiumSubScene subScene = subSceneGameObject.AddComponent<CesiumSubScene>();
            subScene.SetOriginEarthCenteredEarthFixed(
                positionECEF.x,
                positionECEF.y,
                positionECEF.z);

            // Prompt the user to rename the subscene once the hierarchy has updated.
            Selection.activeGameObject = subSceneGameObject;
            EditorApplication.hierarchyChanged += RenameObject;

            return subScene;
        }

        public static void PlaceSubSceneAtCameraPosition(CesiumSubScene subscene)
        {
            CesiumGeoreference? georeference =
                subscene.gameObject.GetComponentInParent<CesiumGeoreference>();
            if (georeference == null)
            {
                throw new InvalidOperationException("CesiumSubScene is not nested inside a game " +
                    "object with a CesiumGeoreference.");
            }

            Undo.RecordObject(subscene, "Place Sub-Scene Origin at Camera Position");
            
            CesiumVector3 positionECEF =
                CesiumEditorUtility.TransformCameraPositionToEarthCenteredEarthFixed(georeference);
            subscene.SetOriginEarthCenteredEarthFixed(
                    positionECEF.x,
                    positionECEF.y,
                    positionECEF.z);
            CesiumEditorUtility.SetSceneViewPositionRotation(
                Vector3.zero, SceneView.lastActiveSceneView.rotation);
        }

        public static void RenameObject()
        {
            EditorApplication.hierarchyChanged -= RenameObject;
            EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
            EditorApplication.ExecuteMenuItem("Edit/Rename");
        }
    }
}