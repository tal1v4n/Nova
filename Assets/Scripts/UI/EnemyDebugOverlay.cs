using System.Collections.Generic;
using Nova.Core;
using Nova.Enemy;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nova.UI
{
    // Overlay de depuración del Enemigo (12.5.2): con F1 en Play enseña qué decidió en el último ciclo,
    // sus mejores candidatas con la puntuación ya con ruido, el umbral y la Paciencia que le queda.
    // Sirve para ajustar los perfiles en Nova → Dificultad sabiendo qué número tocar.
    //
    // Es una herramienta de desarrollo, no UI del juego: IMGUI, sin arte y con los textos aquí y no en
    // UiText. Solo existe en el Editor y en las builds de desarrollo, y se crea sola en cada escena que
    // tenga un EnemyController, sin tocar la escena.
    [DisallowMultipleComponent]
    public sealed class EnemyDebugOverlay : MonoBehaviour
    {
        const int Shown = 6;
        const int History = 6;

        EnemyController enemy;
        bool visible;
        GUIStyle style;
        readonly List<string> history = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!Debug.isDebugBuild)
                return;
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        static void Attach()
        {
            EnemyController controller = FindAnyObjectByType<EnemyController>();
            if (controller == null || FindAnyObjectByType<EnemyDebugOverlay>() != null)
                return;
            var go = new GameObject("Enemy Debug Overlay");
            go.AddComponent<EnemyDebugOverlay>().enemy = controller;
        }

        void OnEnable()
        {
            if (enemy != null)
                enemy.OnDecided += Record;
        }

        void Start()
        {
            if (enemy != null)
            {
                enemy.OnDecided -= Record;
                enemy.OnDecided += Record;
            }
        }

        void OnDisable()
        {
            if (enemy != null)
                enemy.OnDecided -= Record;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        void Record(EnemyAction? action, bool executed)
        {
            string line = $"{enemy.MatchSeconds,6:0.0}s  "
                + (action.HasValue ? $"{Describe(action.Value)}  {action.Value.Score:0}{(executed ? "" : "  (rechazada)")}" : "— espera");
            if (enemy.LastWasDefense)
                line += "  [defensa]";
            history.Insert(0, line);
            if (history.Count > History)
                history.RemoveAt(history.Count - 1);
        }

        void OnGUI()
        {
            if (!visible || enemy == null || enemy.Profile == null)
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = false };

            var lines = new List<string>
            {
                $"<b>ENEMIGO · {enemy.Profile.name}</b>   (F1 oculta · ajustes en Nova → Dificultad)",
                $"Intervalo {enemy.Profile.DecisionInterval:0.##} s · Umbral {enemy.Profile.ActionThreshold:0.#} · "
                    + $"Agresividad {enemy.Profile.AggressionMultiplier:0.##} · Ruido ±{enemy.Profile.DecisionNoise * 100f:0}%",
                enemy.IsHostile
                    ? $"<color=#ff8080>Hostil</color> · {enemy.MatchSeconds:0} s de partida · {enemy.Cycles} ciclos"
                    : $"<color=#80ff80>Paciente</color>: le quedan {enemy.GraceRemaining:0.0} s sin atacarte",
                "",
                "<b>Último ciclo</b> (puntuación con ruido; en verde, las que superan el umbral)",
            };

            IReadOnlyList<EnemyAction> candidates = enemy.LastCandidates;
            if (enemy.LastWasDefense && enemy.LastAction.HasValue)
                lines.Add($"  Defensa (12.4): {Describe(enemy.LastAction.Value)}");
            else if (candidates.Count == 0)
                lines.Add("  Sin candidatas: no tiene nada que hacer.");
            for (int i = 0; i < candidates.Count && i < Shown; i++)
            {
                EnemyAction c = candidates[i];
                string color = c.Score > enemy.Profile.ActionThreshold ? "#80ff80" : "#a0a0a0";
                string mark = i == 0 && enemy.LastAction.HasValue ? "▶" : " ";
                lines.Add($"  {mark} <color={color}>{c.Score,6:0.0}</color>  {Describe(c)}");
            }
            if (candidates.Count > Shown)
                lines.Add($"    … y {candidates.Count - Shown} más");

            lines.Add("");
            lines.Add("<b>Últimas decisiones</b>");
            foreach (string line in history)
                lines.Add("  " + line);

            string text = string.Join("\n", lines);
            Vector2 size = style.CalcSize(new GUIContent(text));
            var area = new Rect(12f, 12f, size.x + 24f, size.y + 16f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(area.x + 12f, area.y + 8f, size.x, size.y), text, style);
        }

        static string Describe(EnemyAction action)
        {
            string actor = Node(action.Actor);
            string target = action.Target >= 0 ? Node(action.Target) : "";
            switch (action.Kind)
            {
                case EnemyActionKind.Attack:
                    bool neutral = NodeAt(action.Target)?.Faction == Faction.Neutral;
                    return $"{(neutral ? "Expandir" : "Atacar")} {actor} → {target}{(action.FullSend ? " (100 %)" : "")}";
                case EnemyActionKind.Supply: return $"Abastecer {actor} → {target}";
                case EnemyActionKind.Reinforce: return $"Reforzar {actor} → {target}";
                case EnemyActionKind.Evacuate: return $"Evacuar {actor} → {target}";
                case EnemyActionKind.Ignite: return $"Encender {actor} como {TypeName(action.NodeType)}";
                case EnemyActionKind.Upgrade: return $"Mejorar {actor}";
                case EnemyActionKind.ChargePower: return $"Cargar {PowerName(action.Power)} en {actor}";
                case EnemyActionKind.LaunchPower: return $"Lanzar {PowerName(action.Power)} {actor} → {target}";
                case EnemyActionKind.Convert: return $"Reconvertir {actor} en {TypeName(action.NodeType)}";
                default: return action.ToString();
            }
        }

        static NodeController NodeAt(int index)
        {
            NodeRegistry registry = NodeRegistry.Instance;
            if (registry == null || index < 0 || index >= registry.All.Count)
                return null;
            return registry.All[index];
        }

        static string Node(int index)
        {
            NodeController node = NodeAt(index);
            if (node == null)
                return $"#{index}";
            string faction = node.Faction == Faction.Player ? "J" : node.Faction == Faction.Enemy ? "E" : "N";
            string level = node.Type == NodeType.Asteroid ? "" : $"({node.Level})";
            return $"{TypeName(node.Type)}{level} {faction} {node.Energy}";
        }

        static string TypeName(NodeType type)
        {
            switch (type)
            {
                case NodeType.Star: return "Estrella";
                case NodeType.Pulsar: return "Púlsar";
                case NodeType.Magnetar: return "Magnetar";
                default: return "Asteroide";
            }
        }

        static string PowerName(PowerType power)
        {
            switch (power)
            {
                case PowerType.AbsoluteZero: return "Cero Absoluto";
                case PowerType.Decay: return "Decaimiento";
                default: return "Agujero de Gusano";
            }
        }
    }
}
