namespace Nova.Presentation
{
    // Orden de dibujo del tablero (SpriteRenderer.sortingOrder), en un solo sitio desde T6-02. Hasta
    // entonces cada vista llevaba su número suelto, y el sprite del nodo (0) y su Energía (1) iban
    // seguidos: no quedaba hueco para las capas 2–4 de 14.7. Del número de Energía hacia arriba todo sube
    // 5, sin cambiar el orden relativo, y las capas del nodo coinciden con la numeración de 14.7.
    //
    // El fondo (BackgroundView, −100) y la UI de partida (20 y 30) quedan fuera: no se cruzan con esto.
    public static class BoardOrder
    {
        // Detrás del nodo: el radio del Magnetar y el halo del drop, y la selección y la línea de envío
        // (capa 0 de 14.7), que asoman por fuera del sprite.
        public const int BehindNode = -2;
        public const int Strokes = -1;

        // 14.7, capa 1: el sprite base.
        public const int Body = 0;

        // 14.7, capa 2: los tintes. Congelado por encima de Reconvirtiendo («Congelado > Reconvirtiendo»).
        public const int ConvertTint = 1;
        public const int FrozenTint = 2;

        // 14.7, capa 3: el aura de Infectado.
        public const int InfectedAura = 3;

        // 14.7, capa 4: los glifos, fuera del cuerpo (zzz, brasa, galón del buff).
        public const int Glyph = 4;

        // 14.7, capa 6: la Energía, y encima la barra de carga del Púlsar.
        public const int Energy = 6;
        public const int ChargeBar = 7;
        public const int ChargeFill = 8;

        // El velo del apuntado tapa nodo y número, como la barra.
        public const int AimVeil = 7;

        // La estela del protón (T6-04) es un trazo de mundo de la capa 0: cerca de su origen pasa por
        // debajo del nodo que lo lanzó, como la línea de envío, y nunca tapa un número de Energía.
        public const int ProtonTrail = Strokes;

        // Los protones pasan por encima de los nodos y su número de Carga por encima del protón.
        public const int Proton = 7;
        public const int ProtonCharge = 8;

        // 14.7, capa 7: los efectos puntuales de A.5 (VFXPool, T6-07), por encima de los protones y de su Carga:
        // el destello del disparo cae sobre el número del protón alcanzado, sin pisarlo.
        public const int PointEffect = 9;

        // Y encima de ellos, Capturado (ART-ST-CAP), que NodeAnimator dibuja colgado del nodo. Va encima a
        // propósito: el impacto del Agujero de Gusano y la cascada de conquista arrancan en el mismo frame que
        // él y se dibujan debajo de sus dos anillos (A.5). Desde T6-07 todo lo de aquí arriba sube 1.
        public const int NodeEffect = 10;

        // Por encima de los protones: el disparo y el haz provisionales, rectas de color liso. Con arte, el
        // arco del Magnetar (T6-05) y el haz de poder (T6-11) son trazos de la capa 0 (Strokes).
        public const int MagnetarShot = 11;
        public const int Beam = 12;

        // T6-06. El anillo de selección va detrás del nodo y asoma por fuera, como el radio del Magnetar, pero
        // encima de él: un Magnetar propio seleccionado enseña los dos (A.4.3). La retícula de apuntado va sobre
        // el objetivo y el lazo sobre todo el tablero, por debajo de la UI de partida (20 y 30).
        public const int SelectionRing = BehindNode + 1;
        public const int AimReticle = 13;
        public const int Lasso = 14;
    }
}
