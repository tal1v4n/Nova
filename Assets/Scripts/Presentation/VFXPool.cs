using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-07: los quince efectos puntuales de 14.5 con su arte (A.5), desde un pool de SpriteRenderer. Solo lee
    // (14.0 #1): escucha los eventos de la lógica y elige la fila de cada hoja por su nombre (VfxArt).
    //
    // - Sobre el protón: ART-FX-SHOT cuelga del protón que sobrevive al disparo del Magnetar, con la fila de su
    //   bando y su talla y una de sus 4 variantes al azar. ART-FX-KILL y ART-FX-VANISH lo sustituyen en su
    //   sitio cuando muere derribado o se queda huérfano, desde el frame de su bucle (ProtonVisual.OnEnded).
    // - Sobre el nodo, con la talla de «La órbita» por su huella (InputArt.SizeFor, también el Asteroide):
    //   ART-FX-SEND en el origen de cada protón; ART-FX-ABS y ART-FX-DMG en el destino, con el bando del protón
    //   y, el daño, una de sus 4 variantes; ART-FX-IGN al encenderse; ART-FX-UPG al subir de nivel, con la fila
    //   de su bando, tipo y nivel nuevo; ART-FX-THAW al deshelarse por impacto o por silencio, y ART-FX-CURE al
    //   curarse por Energía, desde el frame en que estaba el aura (NodeAnimator.InfectedLoopFrame).
    // - Los poderes: ART-FX-CAST en el Púlsar en el mismo frame que nace el haz, por poder y nivel; y al llegar,
    //   el impacto de su poder. El de Cero Absoluto y el de Decaimiento acaban en su overlay, así que le piden a
    //   NodeAnimator que no lo dibuje mientras suenan; el del Agujero de Gusano va con Capturado, debajo.
    // - ART-FX-CASCADE, uno por nodo que WinConditionChecker.OnConquered pasa al Jugador.
    //
    // Los efectos de un nodo cuelgan de su sprite, como los overlays: se mueven con el cabeceo y el último frame
    // de un impacto cae encima del overlay que lo releva. Todo en la capa 7 de 14.7 (BoardOrder.PointEffect), por
    // debajo de Capturado. Tiempo escalado: en pausa los efectos se quedan quietos (15.8), y al acabar la cascada
    // el último nodo se queda en su frame 0.
    //
    // Vive en el objeto Systems. Sin VfxArt completo no dibuja nada: la partida se juega igual.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class VFXPool : MonoBehaviour
    {
        const float PixelsPerUnit = 64f;

        [SerializeField] VfxArt art;
        [Tooltip("Las tallas de «La órbita»: la fila de los efectos sobre un nodo es la de su anillo de selección.")]
        [SerializeField] InputArt input;
        [Tooltip("Las tallas del protón, para el destello del disparo.")]
        [SerializeField] ProtonArt protonArt;
        [Tooltip("Avisa de los protones derribados y huérfanos.")]
        [SerializeField] ProtonVisual protonVisual;

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry nodes;
        [Tooltip("Opcional: sin él se usa ProtonRegistry.Instance.")]
        [SerializeField] ProtonRegistry protons;
        [Tooltip("Opcional: sin él se usa PowerBeamSystem.Instance.")]
        [SerializeField] PowerBeamSystem beams;
        [Tooltip("Opcional: sin él se busca en este mismo objeto. Solo existe en las escenas con fin de nivel.")]
        [SerializeField] WinConditionChecker winChecker;

        sealed class Effect
        {
            public SpriteRenderer Renderer;
            public VfxArt.Sheet Sheet;
            public int Row;
            public int Variant;
            public double Age; // en double, como los relojes de NodeAnimator: el relevo de un overlay cae en el mismo frame
            public Proton Follow;
        }

        // Lo que se suscribió en un nodo, para soltarlo.
        sealed class NodeHooks
        {
            public Action Release;
        }

        readonly List<Effect> active = new List<Effect>();
        readonly Stack<SpriteRenderer> free = new Stack<SpriteRenderer>();
        readonly Dictionary<NodeController, NodeHooks> hookedNodes = new Dictionary<NodeController, NodeHooks>();
        readonly HashSet<Proton> seenProtons = new HashSet<Proton>();   // en vuelo y ya vistos
        readonly HashSet<Proton> hookedProtons = new HashSet<Proton>(); // con OnImpact enganchado
        readonly List<Proton> startedProtons = new List<Proton>();
        readonly List<Proton> pendingShots = new List<Proton>();
        readonly System.Random random = new System.Random(7);

        PowerBeamSystem hookedBeams;
        WinConditionChecker hookedChecker;
        ProtonVisual hookedProtonVisual;

        // De InputArt y ProtonArt solo hacen falta las tallas.
        public bool HasArt => art != null && art.IsComplete && input != null && input.RingSizes > 0 &&
                              protonArt != null && protonArt.Sizes > 0;

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(VfxArt vfxArt, InputArt inputArt, ProtonArt protonSizes, ProtonVisual protonView,
            NodeRegistry nodeRegistry = null, ProtonRegistry protonRegistry = null, PowerBeamSystem beamSystem = null,
            WinConditionChecker checker = null)
        {
            Unsubscribe();
            art = vfxArt;
            input = inputArt;
            protonArt = protonSizes;
            protonVisual = protonView;
            nodes = nodeRegistry;
            protons = protonRegistry;
            beams = beamSystem;
            winChecker = checker;
            if (isActiveAndEnabled)
                Subscribe();
        }

        // El fin de nivel se añade a la escena después de los sistemas (TestScenes).
        public void UseWinChecker(WinConditionChecker checker)
        {
            winChecker = checker;
            if (isActiveAndEnabled)
                Subscribe();
        }

        void OnEnable() => Subscribe();

        // Los registros fijan su Instance en Awake; si este OnEnable llegó antes, se engancha aquí.
        void Start() => Subscribe();

        void OnDisable()
        {
            Unsubscribe();
            for (int i = active.Count - 1; i >= 0; i--)
                Release(i);
        }

        void LateUpdate() => Advance(Time.deltaTime);

        // Un paso de presentación. En partida, el tiempo de juego del frame: en pausa no corre nada. Primero
        // avanzan los efectos que ya sonaban; luego empiezan los de este frame, en su frame 0.
        internal void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            Subscribe();
            HookNewNodes();

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Effect effect = active[i];
                effect.Age += deltaTime;
                if (!Show(effect))
                    Release(i);
            }

            ScanProtons();
            PlayPendingShots();
        }

        // --- Suscripciones ---

        internal void Subscribe()
        {
            PowerBeamSystem beamSystem = beams != null ? beams : PowerBeamSystem.Instance;
            if (beamSystem != hookedBeams)
            {
                if (hookedBeams != null)
                {
                    hookedBeams.OnLaunched -= HandleBeamLaunched;
                    hookedBeams.OnLanded -= HandleBeamLanded;
                }
                hookedBeams = beamSystem;
                if (hookedBeams != null)
                {
                    hookedBeams.OnLaunched += HandleBeamLaunched;
                    hookedBeams.OnLanded += HandleBeamLanded;
                }
            }

            WinConditionChecker checker = winChecker != null ? winChecker : GetComponent<WinConditionChecker>();
            if (checker != hookedChecker)
            {
                if (hookedChecker != null)
                    hookedChecker.OnConquered -= HandleConquered;
                hookedChecker = checker;
                if (hookedChecker != null)
                    hookedChecker.OnConquered += HandleConquered;
            }

            if (protonVisual != hookedProtonVisual)
            {
                if (hookedProtonVisual != null)
                    hookedProtonVisual.OnEnded -= HandleProtonEnded;
                hookedProtonVisual = protonVisual;
                if (hookedProtonVisual != null)
                    hookedProtonVisual.OnEnded += HandleProtonEnded;
            }
        }

        void Unsubscribe()
        {
            if (hookedBeams != null)
            {
                hookedBeams.OnLaunched -= HandleBeamLaunched;
                hookedBeams.OnLanded -= HandleBeamLanded;
                hookedBeams = null;
            }
            if (hookedChecker != null)
            {
                hookedChecker.OnConquered -= HandleConquered;
                hookedChecker = null;
            }
            if (hookedProtonVisual != null)
            {
                hookedProtonVisual.OnEnded -= HandleProtonEnded;
                hookedProtonVisual = null;
            }
            foreach (NodeHooks hooks in hookedNodes.Values)
                hooks.Release();
            hookedNodes.Clear();
            foreach (Proton proton in hookedProtons)
            {
                if (proton != null)
                {
                    proton.OnImpact -= HandleImpact;
                    proton.OnFinished -= HandleProtonFinished;
                }
            }
            hookedProtons.Clear();
            seenProtons.Clear();
            pendingShots.Clear();
        }

        // Los nodos no avisan al registrarse: se miran cada frame, como hace ProtonVisual con los protones.
        void HookNewNodes()
        {
            NodeRegistry registry = nodes != null ? nodes : NodeRegistry.Instance;
            if (registry == null)
                return;
            foreach (NodeController node in registry.All)
            {
                if (node != null && !hookedNodes.ContainsKey(node))
                    hookedNodes.Add(node, Hook(node));
            }
        }

        // Para los tests: un nodo sin registro.
        internal void Watch(NodeController node)
        {
            if (node != null && !hookedNodes.ContainsKey(node))
                hookedNodes.Add(node, Hook(node));
        }

        NodeHooks Hook(NodeController node)
        {
            Action<NodeType, NodeType> typeChanged = (previous, current) =>
            {
                // Encender es el único cambio de tipo que sale del Asteroide (4.1): reconvertir nunca (8.1).
                if (previous == NodeType.Asteroid)
                    PlayIgnite(node);
            };
            Action<int, int> levelChanged = (previous, current) =>
            {
                // Mejorar sube un nivel del mismo tipo; encender y reconvertir dejan el nodo en el 1.
                if (current > previous && current >= 2)
                    PlayUpgrade(node);
            };
            node.OnTypeChanged += typeChanged;
            node.OnLevelChanged += levelChanged;

            var freeze = node.GetComponent<NodeFreeze>();
            Action<ThawReason> thawed = reason =>
            {
                // 14.5: la Energía llega a 0 o pasan 30 s. El Agujero de Gusano lo quita al capturar: eso es
                // Capturado, no un deshielo.
                if (HasArt && reason != ThawReason.Wormhole)
                    PlayOnNode(node, art.Thaw, $"thaw_{RadiusOf(node)}");
            };
            if (freeze != null)
                freeze.OnThawed += thawed;

            var decay = node.GetComponent<NodeDecay>();
            Action<CureReason> cured = reason =>
            {
                if (!HasArt || reason != CureReason.Impact)
                    return;
                var animator = node.GetComponent<NodeAnimator>();
                PlayOnNode(node, art.Cure, $"cure_{RadiusOf(node)}", animator != null ? animator.InfectedLoopFrame : 0);
            };
            if (decay != null)
                decay.OnCured += cured;

            var turret = node.GetComponent<MagnetarTurret>();
            Action<MagnetarTurret, Proton> shot = (_, proton) => QueueShot(proton);
            if (turret != null)
                turret.OnShot += shot;

            return new NodeHooks
            {
                Release = () =>
                {
                    if (node == null)
                        return;
                    node.OnTypeChanged -= typeChanged;
                    node.OnLevelChanged -= levelChanged;
                    if (freeze != null)
                        freeze.OnThawed -= thawed;
                    if (decay != null)
                        decay.OnCured -= cured;
                    if (turret != null)
                        turret.OnShot -= shot;
                },
            };
        }

        // Los protones nuevos de este frame: el envío en su origen y, al llegar, la absorción o el daño.
        void ScanProtons()
        {
            ProtonRegistry registry = protons != null ? protons : ProtonRegistry.Instance;
            if (registry == null)
                return;
            startedProtons.Clear();
            foreach (Proton proton in registry.InFlight)
            {
                if (proton != null && proton.InFlight && !seenProtons.Contains(proton))
                    startedProtons.Add(proton);
            }
            foreach (Proton proton in startedProtons)
            {
                seenProtons.Add(proton);
                // Un protón avisa de OnFinished ANTES que de OnImpact (Proton.Impact): si al acabar el vuelo se
                // soltase OnImpact, el impacto no llegaría nunca. Se engancha una vez por objeto y se queda: el
                // pool de protones los reutiliza, así que son pocos.
                if (hookedProtons.Add(proton))
                {
                    proton.OnImpact += HandleImpact;
                    proton.OnFinished += HandleProtonFinished;
                }
                if (HasArt && proton.Source != null)
                    PlayOnNode(proton.Source, art.Send, $"{FactionName(proton.Faction)}_{RadiusOf(proton.Source)}");
            }
        }

        // El vuelo acabó: si el pool lo relanza, es un envío nuevo.
        void HandleProtonFinished(Proton proton) => seenProtons.Remove(proton);

        void HandleImpact(Proton proton, NodeController node, ImpactOutcome outcome)
        {
            if (!HasArt)
                return;
            string row = $"{FactionName(proton.Faction)}_{RadiusOf(node)}";
            if (outcome.Result == ImpactResult.Absorbed)
                PlayOnNode(node, art.Absorb, row);
            else if (outcome.Result == ImpactResult.Damaged)
                PlayOnNode(node, art.Damage, row, random.Next(art.Damage.Variants));
        }

        // El destello es para el protón que sobrevive: el disparo avisa antes de quitarle la Carga, así que se
        // decide al final del frame. Si cayó, ya lo sustituye KILL (A.5).
        internal void QueueShot(Proton proton) => pendingShots.Add(proton);

        void PlayPendingShots()
        {
            if (pendingShots.Count == 0)
                return;
            if (HasArt)
            {
                foreach (Proton proton in pendingShots)
                {
                    if (proton == null || !proton.InFlight)
                        continue;
                    int size = protonArt.SizeFor(proton.Charge) + 1;
                    Play(art.Shot, $"{FactionName(proton.Faction)}_{size}", random.Next(art.Shot.Variants), null,
                        Snap(proton.transform.position), proton);
                }
            }
            pendingShots.Clear();
        }

        internal void HandleProtonEnded(ProtonEnding ending)
        {
            if (!HasArt)
                return;
            string faction = FactionName(ending.Faction);
            if (ending.Fate == ProtonFate.ShotDown)
                Play(art.Kill, faction, ending.LoopFrame, null, ending.Position);
            else
                Play(art.Vanish, $"{faction}_{ending.Size + 1}", ending.LoopFrame, null, ending.Position);
        }

        void HandleBeamLaunched(PowerBeam beam)
        {
            if (!HasArt || beam.Source == null)
                return;
            PlayOnNode(beam.Source, art.Cast, $"{PowerName(beam.Type)}_{beam.Source.Level}");
        }

        // El efecto ya se aplicó cuando llega este aviso (PowerBeamSystem).
        void HandleBeamLanded(PowerBeam beam)
        {
            if (!HasArt || beam.Target == null)
                return;
            NodeController target = beam.Target;
            int radius = RadiusOf(target);
            switch (beam.Type)
            {
                case PowerType.AbsoluteZero:
                    if (PlayOnNode(target, art.PowerZero, $"pwr_zer_{radius}"))
                        HoldOverlay(target, NodeStateFlags.Frozen, art.PowerZero);
                    break;
                case PowerType.Decay:
                    if (PlayOnNode(target, art.PowerDecay, $"pwr_dec_{radius}"))
                        HoldOverlay(target, NodeStateFlags.Infected, art.PowerDecay);
                    break;
                case PowerType.Wormhole:
                    PlayOnNode(target, art.PowerWormhole, $"pwr_wrm_{radius}");
                    break;
            }
        }

        // El overlay espera lo que dura su impacto: el último frame del impacto ya es él.
        void HoldOverlay(NodeController node, NodeStateFlags overlay, VfxArt.Sheet sheet)
        {
            var animator = node.GetComponent<NodeAnimator>();
            if (animator != null)
                animator.HoldOverlay(overlay, sheet.Frames / art.FramesPerSecond);
        }

        void HandleConquered(NodeController node)
        {
            if (HasArt)
                PlayOnNode(node, art.Cascade, $"cascade_{RadiusOf(node)}");
        }

        void PlayIgnite(NodeController node)
        {
            if (HasArt)
                PlayOnNode(node, art.Ignite, $"{FactionName(node.Faction)}_{RadiusOf(node)}");
        }

        void PlayUpgrade(NodeController node)
        {
            if (HasArt)
                PlayOnNode(node, art.Upgrade, $"{FactionName(node.Faction)}_{TypeName(node.Type)}{node.Level}");
        }

        // --- Reproducción ---

        // Un efecto colgado del sprite del nodo, en su centro. Devuelve si sonó.
        bool PlayOnNode(NodeController node, VfxArt.Sheet sheet, string row, int variant = 0)
        {
            if (!HasArt || node == null)
                return false;
            var visual = node.GetComponent<NodeVisual>();
            Transform parent = visual != null ? visual.Body.transform : node.transform;
            return Play(sheet, row, variant, parent, Vector3.zero);
        }

        // Con parent, position es local; sin él, de mundo. Con follow, sigue al protón mientras vuele.
        bool Play(VfxArt.Sheet sheet, string rowName, int variant, Transform parent, Vector3 position, Proton follow = null)
        {
            int row = sheet.Row(rowName);
            if (row < 0 || variant < 0 || variant >= sheet.Variants)
                return false;

            SpriteRenderer renderer = free.Count > 0 ? free.Pop() : Build();
            renderer.transform.SetParent(parent != null ? parent : transform, false);
            renderer.transform.localPosition = parent != null ? position : Vector3.zero;
            if (parent == null)
                renderer.transform.position = position;
            renderer.gameObject.SetActive(true);

            var effect = new Effect { Renderer = renderer, Sheet = sheet, Row = row, Variant = variant, Follow = follow };
            active.Add(effect);
            Show(effect);
            return true;
        }

        // Pinta el frame que toca, o devuelve false si el efecto ya acabó.
        bool Show(Effect effect)
        {
            int frame = (int)Math.Floor(effect.Age * art.FramesPerSecond + 1e-9);
            if (frame >= effect.Sheet.Frames || effect.Renderer == null)
                return false;
            if (effect.Follow != null)
            {
                if (!effect.Follow.InFlight)
                    return false;
                effect.Renderer.transform.position = Snap(effect.Follow.transform.position);
            }
            effect.Renderer.sprite = effect.Sheet.Frame(effect.Row, effect.Variant, frame);
            effect.Renderer.enabled = true;
            return true;
        }

        void Release(int index)
        {
            SpriteRenderer renderer = active[index].Renderer;
            active.RemoveAt(index);
            if (renderer == null)
                return;
            renderer.enabled = false;
            renderer.gameObject.SetActive(false);
            renderer.transform.SetParent(transform, false);
            free.Push(renderer);
        }

        SpriteRenderer Build()
        {
            var renderer = new GameObject("Effect").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sortingOrder = BoardOrder.PointEffect;
            return renderer;
        }

        // --- Nombres de fila (los .json de A.5) ---

        // El radio de «La órbita» que le toca al nodo por su huella: 24, 28, 32, 36 o 40.
        int RadiusOf(NodeController node) => input.RingRadiusPixels(input.SizeFor(NodeVisual.FootprintPixelsOf(node)));

        static string FactionName(Faction faction) =>
            faction == Faction.Player ? "player" : faction == Faction.Enemy ? "enemy" : "neutral";

        static string TypeName(NodeType type)
        {
            switch (type)
            {
                case NodeType.Star:
                    return "str";
                case NodeType.Pulsar:
                    return "pul";
                case NodeType.Magnetar:
                    return "mag";
                default:
                    return "ast";
            }
        }

        static string PowerName(PowerType type)
        {
            switch (type)
            {
                case PowerType.AbsoluteZero:
                    return "zero";
                case PowerType.Decay:
                    return "decay";
                case PowerType.Wormhole:
                    return "wormhole";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Poder sin efecto.");
            }
        }

        static Vector3 Snap(Vector3 position) =>
            new Vector3(Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit, 0f);

        // --- Para los tests ---

        internal int ActiveCount => active.Count;

        // Los efectos que suenan: sprite, padre y posición de mundo.
        internal IEnumerable<SpriteRenderer> Renderers
        {
            get
            {
                foreach (Effect effect in active)
                    yield return effect.Renderer;
            }
        }
    }
}
