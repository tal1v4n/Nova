using System;
using Nova.Data;
using Nova.Enemy;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    // Nova → Dificultad (12.5.2): los perfiles del Enemigo de los nueve niveles en una sola tabla, un
    // nivel por columna y un parámetro por fila, para ajustar la curva a mano y ver de un vistazo si
    // sube. Edita los assets Level{n}Enemy directamente, con Undo; en Play, el Enemigo los lee en cada
    // ciclo y el cambio se nota en la partida en curso, y se conserva al salir de Play.
    //
    // Una celda en rojo es un nivel más fácil que el anterior en ese parámetro. No es un error —se
    // puede querer un respiro—, pero LevelAssetsTests lo trata como tal (12.5.2).
    public sealed class DifficultyWindow : EditorWindow
    {
        // Un parámetro de EnemyProfile: su campo serializado, su etiqueta y hacia dónde es más difícil
        // (+1, más alto; −1, más bajo; 0, sin sentido de curva).
        readonly struct Row
        {
            public readonly string Field;
            public readonly string Label;
            public readonly int Harder;
            public readonly string Tip;

            public Row(string field, string label, int harder, string tip)
            {
                Field = field;
                Label = label;
                Harder = harder;
                Tip = tip;
            }
        }

        static readonly Row[] Rows =
        {
            new Row("decisionInterval", "Intervalo (s)", -1, "Segundos entre decisiones. Menos = piensa más rápido."),
            new Row("actionThreshold", "Umbral", -1, "Puntuación mínima para actuar. Menos = actúa más a menudo."),
            new Row("aggressionMultiplier", "Agresividad", +1, "Multiplica atacar al Jugador y los poderes. Más = más agresivo."),
            new Row("decisionNoise", "Ruido", -1, "± sobre cada puntuación. Más = más torpe."),
            new Row("gracePeriod", "Paciencia (s)", -1, "Segundos sin atacar al Jugador al empezar."),
            new Row("fullSendMargin", "Margen 100 %", 0, "Envía el 100 % si Energía ≥ coste × margen. 0 = nunca."),
            new Row("usesEvacuation", "Evacúa", 0, "Vacía un nodo que va a caer en vez de regalarlo lleno."),
            new Row("usesConversion", "Reconvierte", 0, "Reconvierte un nodo en Estrella si se queda sin ninguna. Lo fija el nivel (11.1)."),
            new Row("prioritizesPowerUps", "Power-ups", 0, "Activa BonusPowerUp al elegir Asteroides."),
        };

        const string ConversionField = "usesConversion";
        const int Levels = 9;
        const float LabelWidth = 110f;
        const float CellWidth = 64f;

        readonly LevelDefinition[] levels = new LevelDefinition[Levels];
        readonly SerializedObject[] profiles = new SerializedObject[Levels];
        Vector2 scroll;
        int from = 1;
        int to = 9;

        [MenuItem("Nova/Dificultad")]
        public static void Open()
        {
            GetWindow<DifficultyWindow>("Dificultad").Load();
        }

        void OnEnable() => Load();
        void OnFocus() => Load();

        // En Play, para la fila de la Paciencia que queda.
        void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        void Load()
        {
            for (int i = 0; i < Levels; i++)
            {
                levels[i] = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelAssets.PathOf(i + 1));
                EnemyProfile profile = levels[i] != null ? levels[i].EnemyProfile : null;
                profiles[i] = profile != null ? new SerializedObject(profile) : null;
            }
        }

        void OnGUI()
        {
            if (Array.Exists(levels, l => l == null))
            {
                EditorGUILayout.HelpBox("Faltan niveles: ejecuta Nova → Fase 5 → Generar los 9 niveles.", MessageType.Warning);
                return;
            }
            if (SharedProfiles(out string shared))
            {
                EditorGUILayout.HelpBox($"{shared} comparten perfil: un cambio afectaría a varios niveles. "
                    + "Ejecuta Nova → Fase 5 → Generar los 9 niveles para crear Level{n}Enemy.", MessageType.Warning);
                if (GUILayout.Button("Generar los 9 niveles"))
                {
                    LevelAssets.Generate();
                    Load();
                }
                return;
            }

            EnemyController enemy = Application.isPlaying ? FindAnyObjectByType<EnemyController>() : null;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawHeader(enemy);
            foreach (Row row in Rows)
                DrawRow(row);
            DrawColumnButtons();
            if (enemy != null)
                DrawLive(enemy);

            EditorGUILayout.Space();
            DrawInterpolation();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Guardar", GUILayout.Width(120f)))
                    AssetDatabase.SaveAssets();
                GUILayout.Label("En rojo: más fácil que el nivel anterior.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        bool SharedProfiles(out string shared)
        {
            for (int i = 0; i < Levels; i++)
            {
                for (int j = i + 1; j < Levels; j++)
                {
                    if (profiles[i] != null && profiles[j] != null && profiles[i].targetObject == profiles[j].targetObject)
                    {
                        shared = $"Los niveles {i + 1} y {j + 1}";
                        return true;
                    }
                }
            }
            shared = null;
            return false;
        }

        void DrawHeader(EnemyController enemy)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("", GUILayout.Width(LabelWidth));
                for (int i = 0; i < Levels; i++)
                {
                    bool playing = enemy != null && profiles[i] != null && enemy.Profile == profiles[i].targetObject;
                    string title = $"{(playing ? "▶ " : "")}N{i + 1} · M{levels[i].Map}";
                    GUILayout.Label(title, playing ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(CellWidth));
                }
            }
        }

        void DrawRow(Row row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(row.Label, row.Tip), GUILayout.Width(LabelWidth));
                for (int i = 0; i < Levels; i++)
                {
                    SerializedObject profile = profiles[i];
                    if (profile == null)
                    {
                        GUILayout.Label("—", GUILayout.Width(CellWidth));
                        continue;
                    }
                    profile.Update();
                    SerializedProperty property = profile.FindProperty(row.Field);
                    Color previous = GUI.backgroundColor;
                    if (i > 0 && Easier(row, i))
                        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                    if (row.Field == ConversionField)
                    {
                        // 11.1 y 12.5: reconvierte justo donde el nivel deja reconvertir, a los dos bandos.
                        property.boolValue = levels[i].AllowsConversion;
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(CellWidth));
                    }
                    else if (property.propertyType == SerializedPropertyType.Boolean)
                        property.boolValue = EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(CellWidth));
                    else
                        property.floatValue = Clamp(row, EditorGUILayout.FloatField(property.floatValue, GUILayout.Width(CellWidth)));
                    GUI.backgroundColor = previous;
                    profile.ApplyModifiedProperties();
                }
            }
        }

        // El nivel i es más fácil que el i − 1 en ese parámetro.
        bool Easier(Row row, int i)
        {
            if (row.Harder == 0 || profiles[i - 1] == null)
                return false;
            float now = profiles[i].FindProperty(row.Field).floatValue;
            float before = profiles[i - 1].FindProperty(row.Field).floatValue;
            return row.Harder > 0 ? now < before - 1e-4f : now > before + 1e-4f;
        }

        void DrawColumnButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Copiar →", "Copia la columna al nivel siguiente."), GUILayout.Width(LabelWidth));
                for (int i = 0; i < Levels; i++)
                {
                    using (new EditorGUI.DisabledScope(i == Levels - 1))
                    {
                        if (GUILayout.Button("→", GUILayout.Width(CellWidth)))
                            Copy(i, i + 1);
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Restablecer", "Vuelve a los valores de 12.5 del mapa (Map1–Map3) y la Paciencia de 12.5.1."),
                    GUILayout.Width(LabelWidth));
                for (int i = 0; i < Levels; i++)
                {
                    if (GUILayout.Button("12.5", GUILayout.Width(CellWidth)))
                        Reset(i);
                }
            }
        }

        void DrawLive(EnemyController enemy)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("En juego", EditorStyles.boldLabel, GUILayout.Width(LabelWidth));
                GUILayout.Label($"{enemy.MatchSeconds:0.0} s de partida · Paciencia restante {enemy.GraceRemaining:0.0} s · "
                    + $"{(enemy.IsHostile ? "hostil" : "paciente")} · {enemy.Cycles} ciclos");
            }
        }

        void DrawInterpolation()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Interpolar", "Reparte en línea recta los valores numéricos entre dos niveles. Los sí/no no se tocan."),
                    GUILayout.Width(LabelWidth));
                from = EditorGUILayout.IntSlider(from, 1, Levels, GUILayout.Width(180f));
                GUILayout.Label("→", GUILayout.Width(16f));
                to = EditorGUILayout.IntSlider(to, 1, Levels, GUILayout.Width(180f));
                using (new EditorGUI.DisabledScope(to - from < 2))
                {
                    if (GUILayout.Button("Interpolar", GUILayout.Width(100f)))
                        Interpolate(from - 1, to - 1);
                }
            }
        }

        void Copy(int source, int target)
        {
            foreach (Row row in Rows)
                Set(target, row, Get(source, row));
        }

        void Reset(int index)
        {
            var reference = AssetDatabase.LoadAssetAtPath<EnemyProfile>($"Assets/Data/EnemyProfiles/Map{levels[index].Map}.asset");
            if (reference == null)
                return;
            var source = new SerializedObject(reference);
            foreach (Row row in Rows)
            {
                SerializedProperty property = source.FindProperty(row.Field);
                Set(index, row, property.propertyType == SerializedPropertyType.Boolean ? (property.boolValue ? 1f : 0f) : property.floatValue);
            }
            Set(index, Array.Find(Rows, r => r.Field == "gracePeriod"), LevelAssets.GracePeriods[index]);
        }

        void Interpolate(int first, int last)
        {
            foreach (Row row in Rows)
            {
                if (profiles[first].FindProperty(row.Field).propertyType == SerializedPropertyType.Boolean)
                    continue;
                float a = Get(first, row);
                float b = Get(last, row);
                for (int i = first + 1; i < last; i++)
                    Set(i, row, (float)Math.Round(Mathf.Lerp(a, b, (float)(i - first) / (last - first)), 2));
            }
        }

        // Los mismos límites que los atributos de EnemyProfile, que un FloatField no respeta.
        static float Clamp(Row row, float value)
        {
            switch (row.Field)
            {
                case "decisionInterval": return Mathf.Max(0.02f, value);
                case "decisionNoise": return Mathf.Clamp01(value);
                default: return Mathf.Max(0f, value);
            }
        }

        float Get(int index, Row row)
        {
            SerializedProperty property = profiles[index].FindProperty(row.Field);
            return property.propertyType == SerializedPropertyType.Boolean ? (property.boolValue ? 1f : 0f) : property.floatValue;
        }

        void Set(int index, Row row, float value)
        {
            SerializedObject profile = profiles[index];
            profile.Update();
            SerializedProperty property = profile.FindProperty(row.Field);
            if (row.Field == ConversionField)
                property.boolValue = levels[index].AllowsConversion;
            else if (property.propertyType == SerializedPropertyType.Boolean)
                property.boolValue = value > 0.5f;
            else
                property.floatValue = Clamp(row, value);
            profile.ApplyModifiedProperties();
        }
    }
}
