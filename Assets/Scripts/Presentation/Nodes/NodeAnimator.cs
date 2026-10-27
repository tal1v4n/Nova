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
    // El susto (ojos y sacudida definitiva) es de T6-03: mientras, se suma aquí el temblor provisional.
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

        const int PixelsPerUnit = 64;

        [SerializeField] BalanceConfig balance;

        [Tooltip("Los overlays de A.3. Lo rellena Nova → Arte → Asignar sprites de nodo.")]
        [SerializeField] NodeStateArt states;

        NodeController node;
        NodeVisual visual;
        NodeConverter converter;
        bool converterLooked;
        ProvisionalNodeView provisional;
        bool provisionalLooked;

        SpriteRenderer convertTint;
        SpriteRenderer frozenTint;
        SpriteRenderer infectedAura;
        SpriteRenderer glyph;
        SpriteRenderer buff;
        SpriteRenderer effect;

        double clock;              // segundos de los bucles de estado
        double idleClock;          // segundos de idle: no corre congelado
        double captureClock = -1;  // segundos de Capturado, o < 0 si no se está reproduciendo
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

        static int FrameAt(double seconds) => (int)Math.Floor(seconds * FramesPerSecond + 1e-9);

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

            // Cabeceo y temblor: se mueve el sprite, y con él lo que cuelga de él.
            int bob = converting ? 0 : BobPixels(Node.Type, (int)(idleTick % BobCycleFrames));
            Vector3 shake = Provisional != null ? Provisional.CurrentShake : Vector3.zero;
            Visual.Body.transform.localPosition = new Vector3(0f, bob / (float)PixelsPerUnit, 0f) + shake;

            if (!DrawsStates)
            {
                HideOverlays();
                return;
            }

            // Capa 2: los tintes.
            Show(convertTint, convertFrame >= 0 ? convertFrames[convertFrame] : null);
            Show(frozenTint, Node.HasState(NodeStateFlags.Frozen) ? states.Frozen : null);

            // Capa 3: el aura.
            Show(infectedAura, Node.HasState(NodeStateFlags.Infected) ? Loop(states.Decay, tick) : null);

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

        // Opcional: de él sale el temblor provisional del susto hasta T6-03.
        ProvisionalNodeView Provisional
        {
            get
            {
                if (!provisionalLooked)
                {
                    provisional = GetComponent<ProvisionalNodeView>();
                    provisionalLooked = true;
                }
                return provisional;
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
