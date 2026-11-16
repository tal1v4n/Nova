using System;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-02: los estados de 14.1 sobre el sprite de NodeVisual, y el idle de los cuatro nodos. Solo lee
    // (14.0 #1): banderas, Energía, facción, la reconversión en curso y los buffs de la facción.
    //
    // - Idle (Normal, y el reposo del Asteroide): la hoja de 16 frames de la Estrella y el Púlsar, y el
    //   cabeceo de píxeles enteros del Asteroide (1 px) y el Magnetar (2 px). Todo a 12 fps y desfasado
    //   por la posición del nodo, con la coreografía de A.2.5; su referencia es tools/preview_idle.py.
    // - Congelado detiene el idle en el frame en que estaba, cabeceo incluido («idle detenido», 14.1).
    // - Reconvirtiendo: la hoja de 36 frames sigue al reloj de NodeConverter, no a uno propio, así que se
    //   pausa con él si lo congelan. En el frame 16 la celda está tapada y el sprite pasa al del tipo de
    //   destino a nivel 1 (A.3.6): el overlay solo suma píxeles, el corte lo pone el animador.
    // - Capturado: 5 frames sobre el sprite ya recoloreado, que NodeVisual cambia en el frame 0 (A.3.7).
    // - Glifos de la capa 4: zzz (Dormido), brasa quieta o abierta según energy >= IgnitionCost
    //   (SinEncender, A.3.2) y el galón del power-up sobre los tipos a los que afecta (A.3.8).
    //
    // Lo que se mueve es el sprite con sus overlays, que cuelgan de él: el nodo no, así que el collider,
    // el número y la barra se quedan quietos (A.2.5). Tiempo escalado: en pausa el tablero se para (15.8).
    //
    // T6-03: el susto (14.2) es la capa 5 de 14.7, sin asset (T.7 #55): un salto corto hacia arriba al
    // empezar y un temblor de 1 px a los lados mientras dure, en píxeles enteros y sumados al cabeceo.
    // Corre en tiempo escalado y no se para con el hielo: un nodo congelado y asustado tiene que avisar (14.6).
    //
    // T6-07: los impactos de poder relevan a sus overlays (A.5). Mientras suena el de Cero Absoluto o el de
    // Decaimiento, VFXPool le pide al animador que no dibuje el hielo o el aura (HoldOverlay): el último frame
    // del impacto ya es el overlay, y el overlay entra en el frame siguiente. Por eso el aura lleva su propio
    // reloj, que arranca en el frame 0 al infectarse y al acabar la espera, y no el del idle desfasado por la
    // posición. Y la cura arranca del frame en que estaba el aura (InfectedLoopFrame).
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController), typeof(NodeVisual))]
    public sealed class NodeAnimator : MonoBehaviour
    {
        // Cadencia de todo el proyecto (14.5) y frames de un bucle idle (NOVA_Arte_Tecnica.md, Animación).
        public const int FramesPerSecond = 12;
        public const int IdleFrames = 16;

        // Cabeceo: el doble de lento que una hoja (A.2.5).
        public const int BobCycleFrames = 32;

        // Frame de ART-ST-CONV con la celda llena: aquí cambia el sprite (A.3.6).
        public const int ConvertSwapFrame = 16;

        // T6-03, el susto (14.2): temblor de 1 px a cada lado, un paso por frame, y el salto, en píxeles
        // hacia arriba frame a frame desde que empieza. Arranca en el pico, como todo lo que se dispara (A.3.7).
        public const int ScareShakePixels = 1;
        static readonly int[] ScareHopPixels = { 4, 3, 2, 1 };
        public static int ScareHopFrames => ScareHopPixels.Length;

        const int PixelsPerUnit = 64;

        [SerializeField] BalanceConfig balance;

        [Tooltip("Los overlays de A.3. Lo rellena Nova → Arte → Asignar sprites de nodo.")]
        [SerializeField] NodeStateArt states;

        NodeController node;
        NodeVisual visual;
        NodeConverter converter;
        bool converterLooked;

        SpriteRenderer convertTint;
        SpriteRenderer frozenTint;
        SpriteRenderer infectedAura;
        SpriteRenderer glyph;
        SpriteRenderer buff;
        SpriteRenderer effect;

        double clock;              // segundos de los bucles de estado
        double idleClock;          // segundos de idle: no corre congelado
        double captureClock = -1;  // segundos de Capturado, o < 0 si no se está reproduciendo
        double scareClock = -1;    // segundos de Asustado, o < 0 si no lo está
        double infectedClock;      // segundos del aura de Infectado: su frame 0 es el de infectarse o el del relevo
        bool wasInfected;
        double frozenHold;         // segundos que el hielo espera a que acabe su impacto (T6-07)
        double infectedHold;       // y el aura al suyo
        int infectedLoopFrame;     // el frame del aura en el último Apply
        bool subscribed;

        public void Configure(BalanceConfig config, NodeStateArt stateArt)
        {
            balance = config;
            states = stateArt;
        }

        // Si dibuja los estados. Sin el arte de A.3, la vista provisional sigue tiñendo el sprite.
        public bool DrawsStates => states != null && states.IsComplete;

        // Para los tests.
        internal SpriteRenderer ConvertTint { get { EnsureBuilt(); return convertTint; } }
        internal SpriteRenderer FrozenTint { get { EnsureBuilt(); return frozenTint; } }
        internal SpriteRenderer InfectedAura { get { EnsureBuilt(); return infectedAura; } }
        internal SpriteRenderer Glyph { get { EnsureBuilt(); return glyph; } }
        internal SpriteRenderer Buff { get { EnsureBuilt(); return buff; } }
        internal SpriteRenderer Effect { get { EnsureBuilt(); return effect; } }
        internal bool IsCapturePlaying => captureClock >= 0;

        // El frame del bucle de Infectado que se dibujó por última vez, de 0 a 15. La cura (ART-FX-CURE) arranca
        // de él: sigue valiendo en el tick en que la bandera se quita, que es cuando se pregunta.
        public int InfectedLoopFrame => infectedLoopFrame;

        // T6-07: no dibujar el overlay de Congelado o de Infectado durante tantos segundos, porque lo releva el
        // impacto de su poder, que acaba en él (A.5). Al terminar, el aura arranca en su frame 0. Otra bandera
        // no tiene impacto que la releve.
        public void HoldOverlay(NodeStateFlags overlay, float seconds)
        {
            if (seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "La espera no es negativa.");
            if (overlay == NodeStateFlags.Frozen)
                frozenHold = seconds;
            else if (overlay == NodeStateFlags.Infected)
                infectedHold = seconds;
            else
                throw new ArgumentException("Solo Congelado e Infectado tienen un impacto que los releve.", nameof(overlay));
            Apply();
        }

        internal bool IsHolding(NodeStateFlags overlay) =>
            overlay == NodeStateFlags.Frozen ? frozenHold > 0 : overlay == NodeStateFlags.Infected && infectedHold > 0;
        internal bool IsScarePlaying => scareClock >= 0;

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        // LateUpdate y no Update: cualquier Refresh de NodeVisual de este frame, que vuelve al sprite base,
        // ya ha pasado, y lo que se dibuja es lo de aquí.
        void LateUpdate()
        {
            Advance(Time.deltaTime);
        }

        // Avanza los relojes y pinta. Público para los tests de EditMode, donde no hay LateUpdate.
        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            clock += deltaTime;
            if (Node.IsInitialized && !Node.HasState(NodeStateFlags.Frozen))
                idleClock += deltaTime;
            // El susto empieza en su frame 0 en el primer Advance que ve la bandera.
            if (Node.IsInitialized && Node.HasState(NodeStateFlags.Scared))
                scareClock = scareClock < 0 ? 0 : scareClock + deltaTime;
            else
                scareClock = -1;
            // El aura: su reloj arranca al infectarse. Si espera a su impacto, arranca al acabar la espera, con lo
            // que se haya pasado de ella este frame, para ir al paso del efecto que la releva.
            bool infected = Node.IsInitialized && Node.HasState(NodeStateFlags.Infected);
            infectedClock = infected && wasInfected ? infectedClock + deltaTime : 0;
            wasInfected = infected;
            if (infectedHold > 0)
            {
                infectedHold -= deltaTime;
                if (HoldEnded(infectedHold))
                {
                    infectedClock = Math.Max(0, -infectedHold);
                    infectedHold = 0;
                }
            }
            if (frozenHold > 0)
            {
                frozenHold -= deltaTime;
                if (HoldEnded(frozenHold))
                    frozenHold = 0;
            }

            if (captureClock >= 0)
            {
                captureClock += deltaTime;
                if (states == null || FrameAt(captureClock) >= states.Captured.Length)
                    captureClock = -1;
            }
            Apply();
        }

        // Frame de arranque de un nodo, por su posición en píxeles de mundo (A.2.5). La traducción literal
        // de desfase() en tools/preview_idle.py: hash de 32 bits, multiplicar y xor, y el finalizador de
        // MurmurHash3, sin el cual los nodos en múltiplos de 0.25 u (16 px) salían todos con fase 0.
        public static uint IdlePhase(Vector3 position)
        {
            int px = Mathf.RoundToInt(position.x * PixelsPerUnit);
            int py = Mathf.RoundToInt(position.y * PixelsPerUnit);
            unchecked
            {
                uint h = (uint)(px * 73856093) ^ (uint)(py * 19349663);
                h ^= h >> 16;
                h *= 0x85EBCA6B;
                h ^= h >> 13;
                h *= 0xC2B2AE35;
                return h ^ (h >> 16);
            }
        }

        // Desplazamiento vertical del cabeceo en ese frame, en píxeles enteros (A.2.5): cabeceo() en
        // tools/preview_idle.py. Solo el Asteroide y el Magnetar cabecean.
        public static int BobPixels(NodeType type, int frame)
        {
            int amplitude = type == NodeType.Asteroid ? 1 : type == NodeType.Magnetar ? 2 : 0;
            return Mathf.RoundToInt(amplitude * Mathf.Sin(2f * Mathf.PI * frame / BobCycleFrames));
        }

        // Desplazamiento del susto en ese frame, en píxeles enteros (14.2): x, el temblor, a la derecha en
        // los pares y a la izquierda en los impares; y, el salto, que acaba en 0 tras ScareHopFrames.
        public static Vector2Int ScarePixels(int frame)
        {
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame), frame, "El susto empieza en el frame 0.");
            int shake = (frame & 1) == 0 ? ScareShakePixels : -ScareShakePixels;
            int hop = frame < ScareHopPixels.Length ? ScareHopPixels[frame] : 0;
            return new Vector2Int(shake, hop);
        }

        static int FrameAt(double seconds) => (int)Math.Floor(seconds * FramesPerSecond + 1e-9);

        // La espera acaba en el mismo frame en que acaba el impacto que la pidió: con la tolerancia de FrameAt.
        static bool HoldEnded(double remaining) => remaining * FramesPerSecond <= 1e-9;

        static T Loop<T>(T[] frames, long frame) => frames[(int)(frame % frames.Length)];

        void Apply()
        {
            EnsureBuilt();
            bool art = Node.IsInitialized && NodeVisual.SpriteOf(Node) != null;
            if (!art)
            {
                HideOverlays();
                return;
            }

            uint phase = IdlePhase(transform.position);
            long tick = FrameAt(clock) + (long)phase;
            long idleTick = FrameAt(idleClock) + (long)phase;

            // Reconversión: el frame lo marca el reloj de la lógica.
            bool converting = Node.HasState(NodeStateFlags.Converting) && Converter != null && Converter.IsConverting;
            Sprite[] convertFrames = converting && DrawsStates ? states.ConvertFor(Node.Faction) : null;
            int convertFrame = convertFrames != null
                ? Mathf.Clamp(FrameAt(Converter.Elapsed), 0, convertFrames.Length - 1) : -1;

            // Capa 1: el sprite. El de destino tras el corte, la hoja de idle en reposo, o el base.
            Sprite body = NodeVisual.SpriteOf(Node);
            NodeLevelArt levelArt;
            if (convertFrame >= ConvertSwapFrame && Converter.TargetDefinition != null
                && Converter.TargetDefinition.TryGetArt(1, out levelArt) && levelArt.SpriteFor(Node.Faction) != null)
            {
                body = levelArt.SpriteFor(Node.Faction);
            }
            else if (!converting && Node.Definition.TryGetArt(Node.Level, out levelArt))
            {
                Sprite[] idle = levelArt.IdleFor(Node.Faction);
                if (idle.Length > 0)
                    body = Loop(idle, idleTick);
            }
            Visual.Body.sprite = body;

            // Cabeceo y susto (capa 5): se mueve el sprite, y con él lo que cuelga de él.
            int bob = converting ? 0 : BobPixels(Node.Type, (int)(idleTick % BobCycleFrames));
            Vector2Int scare = scareClock >= 0 ? ScarePixels(FrameAt(scareClock)) : Vector2Int.zero;
            Visual.Body.transform.localPosition = new Vector3(scare.x, bob + scare.y, 0f) / PixelsPerUnit;

            if (!DrawsStates)
            {
                HideOverlays();
                return;
            }

            // Capa 2: los tintes.
            Show(convertTint, convertFrame >= 0 ? convertFrames[convertFrame] : null);
            Show(frozenTint, Node.HasState(NodeStateFlags.Frozen) && frozenHold <= 0 ? states.Frozen : null);

            // Capa 3: el aura, con su reloj.
            infectedLoopFrame = (int)(FrameAt(infectedClock) % states.Decay.Length);
            Show(infectedAura, Node.HasState(NodeStateFlags.Infected) && infectedHold <= 0
                ? states.Decay[infectedLoopFrame] : null);

            // Capa 4: los glifos.
            Show(glyph, GlyphFor(tick));
            Show(buff, BuffFor());

            // Capa 7: Capturado.
            Show(effect, captureClock >= 0 ? states.Captured[Mathf.Min(FrameAt(captureClock), states.Captured.Length - 1)] : null);
        }

        Sprite GlyphFor(long tick)
        {
            switch (Node.State)
            {
                case NodeState.Sleeping:
                    return Loop(states.Sleep, tick);
                case NodeState.Unignited:
                    return Node.Energy >= Balance.IgnitionCost
                        ? Loop(states.ReadyFor(Node.Faction), tick)
                        : states.UnlitFor(Node.Faction);
                default:
                    return null;
            }
        }

        // A.3.8: el Rayo Cósmico va sobre el Magnetar y el Púlsar, la Nube de Hidrógeno sobre la Estrella, y
        // el Asteroide no lleva ninguno. Por eso un nodo nunca lleva los dos.
        Sprite BuffFor()
        {
            switch (Node.Type)
            {
                case NodeType.Star:
                    return Node.BuffMultiplier(PowerUpStat.EnergyGeneration) > 1f ? states.BuffFor(PowerUpType.HydrogenCloud) : null;
                case NodeType.Pulsar:
                case NodeType.Magnetar:
                    return Node.BuffMultiplier(PowerUpStat.AttackSpeed) > 1f ? states.BuffFor(PowerUpType.CosmicRay) : null;
                default:
                    return null;
            }
        }

        static void Show(SpriteRenderer renderer, Sprite sprite)
        {
            renderer.sprite = sprite;
            renderer.enabled = sprite != null;
        }

        void HideOverlays()
        {
            Show(convertTint, null);
            Show(frozenTint, null);
            Show(infectedAura, null);
            Show(glyph, null);
            Show(buff, null);
            Show(effect, null);
        }

        internal void Subscribe()
        {
            if (subscribed)
                return;
            Node.OnFactionChanged += HandleFactionChanged;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (!subscribed)
                return;
            Node.OnFactionChanged -= HandleFactionChanged;
            subscribed = false;
        }

        // ChangeFaction(): Capturado arranca en su pico, en este mismo frame (A.3.7).
        void HandleFactionChanged(Faction previous, Faction current)
        {
            captureClock = DrawsStates ? 0 : -1;
            Apply();
        }

        // Se resuelven a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
        NodeVisual Visual => visual != null ? visual : visual = GetComponent<NodeVisual>();

        // Opcional en los tests; en partida lo lleva todo nodo (T2-07).
        NodeConverter Converter
        {
            get
            {
                if (!converterLooked)
                {
                    converter = GetComponent<NodeConverter>();
                    converterLooked = true;
                }
                return converter;
            }
        }

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("NodeAnimator necesita BalanceConfig.");

        void EnsureBuilt()
        {
            if (effect != null)
                return;
            Transform parent = Visual.Body.transform;
            convertTint = Layer("ConvertTint", parent, BoardOrder.ConvertTint);
            frozenTint = Layer("FrozenTint", parent, BoardOrder.FrozenTint);
            infectedAura = Layer("InfectedAura", parent, BoardOrder.InfectedAura);
            glyph = Layer("Glyph", parent, BoardOrder.Glyph);
            buff = Layer("Buff", parent, BoardOrder.Glyph);
            effect = Layer("Captured", parent, BoardOrder.NodeEffect);
        }

        static SpriteRenderer Layer(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }
    }
}
