using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    // Capas de física del proyecto (18 #23). «Nodes» es la única nueva: la usa el clic del Jugador
    // (Nova.Input.InputRouter.NodesLayer) contra el trigger de cada nodo. Se comprueba en cada
    // recarga del editor y solo escribe si falta, así que cualquier clon del repositorio la tiene
    // sin pasos a mano.
    [InitializeOnLoad]
    static class ProjectLayers
    {
        const string NodesLayer = Nova.Input.InputRouter.NodesLayer;

        // Primera capa de usuario: fija, para que el número no dependa del orden de creación.
        const int NodesIndex = 8;

        static ProjectLayers()
        {
            EnsureLayer(NodesLayer, NodesIndex);
        }

        static void EnsureLayer(string name, int index)
        {
            if (LayerMask.NameToLayer(name) >= 0)
                return;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                return;

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(slot.stringValue))
            {
                Debug.LogError($"[Nova] La capa {index} ya se llama «{slot.stringValue}»; no se puede crear «{name}» ahí.");
                return;
            }

            slot.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nova] Capa de física «{name}» creada en el índice {index}.");
        }
    }
}
