using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.UI
{
    // Movimiento de las pantallas fuera de partida (13.12): pistas con retardo, duración y curva, y
    // bucles de ambiente. Corre con tiempo no escalado, porque estas pantallas entran con la partida
    // congelada (15.8, Tiempo de la UI y pausa).
    //
    // Cada pista aplica su estado inicial al crearse, así que nada se ve antes de que le toque
    // entrar. Finish lleva todas al final: es lo que ven los tests, que no tienen Update.
    [DisallowMultipleComponent]
    internal sealed class UiMotion : MonoBehaviour
    {
        // La curva orbital de 13.12: sale rápido y se posa.
        public static readonly Func<float, float> Orbital = Bezier(0.2f, 0.8f, 0.2f, 1f);
        // ease de CSS, para los velos.
        public static readonly Func<float, float> Ease = Bezier(0.25f, 0.1f, 0.25f, 1f);
        public static readonly Func<float, float> EaseInOut = Bezier(0.42f, 0f, 0.58f, 1f);
        // El barrido que descubre la curva de poder.
        public static readonly Func<float, float> Reveal = Bezier(0.45f, 0f, 0.2f, 1f);
        public static readonly Func<float, float> Linear = t => t;
        public static readonly Func<float, float> OutCubic = t => 1f - Mathf.Pow(1f - t, 3f);

        sealed class Track
        {
            public float Delay;
            public float Duration;
            public Func<float, float> Curve;
            public Action<float> Apply;
            public bool Done;
        }

        sealed class Loop
        {
            public float Delay;
            public float Period;
            public Action<float> Apply;
        }

        readonly List<Track> tracks = new List<Track>();
        readonly List<Loop> loops = new List<Loop>();
        float time;

        public float Time => time;

        // Una transición: a los delay s empieza y dura duration s. apply recibe el avance ya curvado.
        public void Add(float delay, float duration, Func<float, float> curve, Action<float> apply)
        {
            apply(curve(0f));
            tracks.Add(new Track { Delay = delay, Duration = Mathf.Max(0.0001f, duration), Curve = curve, Apply = apply });
        }

        // Un bucle de ambiente: apply recibe la fase, de 0 a 1, cada period s, desde delay.
        public void Repeat(float delay, float period, Action<float> apply)
        {
            loops.Add(new Loop { Delay = delay, Period = period, Apply = apply });
        }

        void Update()
        {
            Step(UnityEngine.Time.unscaledDeltaTime);
        }

        public void Step(float deltaTime)
        {
            time += deltaTime;
            foreach (Track track in tracks)
            {
                if (track.Done || time < track.Delay)
                    continue;
                float t = Mathf.Clamp01((time - track.Delay) / track.Duration);
                track.Apply(track.Curve(t));
                track.Done = t >= 1f;
            }
            foreach (Loop loop in loops)
            {
                if (time >= loop.Delay)
                    loop.Apply(Mathf.Repeat(time - loop.Delay, loop.Period) / loop.Period);
            }
        }

        // Todo en su sitio final.
        public void Finish()
        {
            foreach (Track track in tracks)
            {
                if (!track.Done)
                    track.Apply(track.Curve(1f));
                track.Done = true;
            }
            time = Mathf.Max(time, LastEnd);
        }

        float LastEnd
        {
            get
            {
                float end = 0f;
                foreach (Track track in tracks)
                    end = Mathf.Max(end, track.Delay + track.Duration);
                return end;
            }
        }

        // cubic-bezier de CSS: resuelve x por Newton y bisección y devuelve y.
        public static Func<float, float> Bezier(float x1, float y1, float x2, float y2)
        {
            return t =>
            {
                if (t <= 0f) return 0f;
                if (t >= 1f) return 1f;
                float u = t;
                for (int i = 0; i < 8; i++)
                {
                    float x = Cubic(u, x1, x2) - t;
                    float dx = CubicSlope(u, x1, x2);
                    if (Mathf.Abs(x) < 1e-5f || Mathf.Abs(dx) < 1e-6f)
                        break;
                    u = Mathf.Clamp01(u - x / dx);
                }
                return Cubic(u, y1, y2);
            };
        }

        static float Cubic(float u, float p1, float p2)
        {
            float v = 1f - u;
            return 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u;
        }

        static float CubicSlope(float u, float p1, float p2)
        {
            float v = 1f - u;
            return 3f * v * v * p1 + 6f * v * u * (p2 - p1) + 3f * u * u * (1f - p2);
        }

        // --- Los patrones de la maqueta ---

        // rise: entra subiendo 32 px y apareciendo, 0.7 s con la orbital.
        public void Rise(RectTransform target, float delay, float duration = 0.7f, float distance = 32f)
        {
            CanvasGroup group = Group(target);
            Vector2 home = target.anchoredPosition;
            Add(delay, duration, Orbital, t =>
            {
                group.alpha = t;
                target.anchoredPosition = home + new Vector2(0f, -distance * (1f - t));
            });
        }

        public void Fade(RectTransform target, float delay, float duration, Func<float, float> curve = null)
        {
            CanvasGroup group = Group(target);
            Add(delay, duration, curve ?? Ease, t => group.alpha = t);
        }

        // drop: cae 16 px desde arriba y aparece.
        public void Drop(RectTransform target, float delay, float duration = 0.5f)
        {
            CanvasGroup group = Group(target);
            Vector2 home = target.anchoredPosition;
            Add(delay, duration, Orbital, t =>
            {
                group.alpha = t;
                target.anchoredPosition = home + new Vector2(0f, 16f * (1f - t));
            });
        }

        public static CanvasGroup Group(RectTransform target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null)
                group = target.gameObject.AddComponent<CanvasGroup>();
            return group;
        }
    }
}
