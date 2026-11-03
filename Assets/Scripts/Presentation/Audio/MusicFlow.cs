using System;

namespace Nova.Presentation
{
    // Qué hace la música de los menús en cada cambio de escena (NOVA_Sonido.md, S.6; T6-13). Lógica pura, sin
    // AudioSource, para que los tests la recorran: MusicDirector le dice qué escena se activa y por dónde va el
    // ciclo, y ejecuta la orden que recibe.
    //
    //   Escena que se activa            Antes        Orden
    //   MainMenu o LevelSelect          en silencio  la intro y, a los 12 s, el ciclo (solo al abrir el juego)
    //   MainMenu o LevelSelect          sonando      nada: sigue sin cortarse
    //   Game                            sonando      fundido de salida
    //   Game                            fundida      nada: Reiniciar, Reintentar, Repetir, Siguiente
    //   MainMenu o LevelSelect          fundida      el principio de la versión siguiente a la que sonaba al
    //                                                lanzar, sin intro y con fundido de entrada (S.7 #6)
    //   cualquier otra (Boot, pruebas)  —            nada
    public enum MusicCueKind
    {
        None,
        StartWithIntro,
        FadeOut,
        ResumeCycle,
    }

    public readonly struct MusicCue
    {
        public readonly MusicCueKind Kind;

        // Solo para ResumeCycle: el segundo del ciclo en que entra, el principio de una versión.
        public readonly double CycleTime;

        public MusicCue(MusicCueKind kind, double cycleTime = 0.0)
        {
            Kind = kind;
            CycleTime = cycleTime;
        }

        public static MusicCue None => new MusicCue(MusicCueKind.None);
    }

    public sealed class MusicFlow
    {
        enum State
        {
            Silent,   // aún no ha sonado: la próxima pantalla de menú abre con la intro
            Playing,  // suena en los menús
            FadedOut, // se fundió al lanzar un nivel y espera a volver
        }

        readonly string gameScene;
        readonly string[] menuScenes;
        readonly double versionLength;
        readonly int versionCount;

        State state = State.Silent;
        int versionAtLaunch;

        public MusicFlow(string gameScene, string[] menuScenes, double versionLength, int versionCount)
        {
            if (versionLength <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(versionLength));
            if (versionCount < 1)
                throw new ArgumentOutOfRangeException(nameof(versionCount));
            this.gameScene = gameScene;
            this.menuScenes = menuScenes ?? Array.Empty<string>();
            this.versionLength = versionLength;
            this.versionCount = versionCount;
        }

        public bool IsPlaying => state == State.Playing;

        // La versión que sonaba al lanzar el último nivel: 0 es la v3, 1 la v1 y 2 la v2.
        public int VersionAtLaunch => versionAtLaunch;

        public double CycleLength => versionLength * versionCount;

        // cyclePosition: el segundo del ciclo que suena ahora. Durante la intro, antes de que arranque el ciclo,
        // vale 0, y la versión que suena es la primera, la v3, porque la intro es la suya.
        public MusicCue OnSceneActivated(string scene, double cyclePosition)
        {
            if (scene == gameScene)
            {
                if (state != State.Playing)
                    return MusicCue.None;
                versionAtLaunch = VersionAt(cyclePosition);
                state = State.FadedOut;
                return new MusicCue(MusicCueKind.FadeOut);
            }

            if (Array.IndexOf(menuScenes, scene) < 0)
                return MusicCue.None;

            switch (state)
            {
                case State.Silent:
                    state = State.Playing;
                    return new MusicCue(MusicCueKind.StartWithIntro);
                case State.FadedOut:
                    state = State.Playing;
                    return new MusicCue(MusicCueKind.ResumeCycle, VersionStart(NextVersion(versionAtLaunch)));
                default:
                    return MusicCue.None;
            }
        }

        public int VersionAt(double cyclePosition)
        {
            if (double.IsNaN(cyclePosition) || double.IsInfinity(cyclePosition) || cyclePosition < 0.0)
                return 0;
            double inCycle = cyclePosition % CycleLength;
            return Math.Min((int)Math.Floor(inCycle / versionLength), versionCount - 1);
        }

        public int NextVersion(int version) => (version + 1) % versionCount;

        public double VersionStart(int version) => version * versionLength;
    }
}
