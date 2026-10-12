using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Enemy
{
    // Un nodo tal como lo ve el algoritmo (12.1, paso 1): lo que dicen las reglas de Core más lo que
    // hace falta para puntuar —posición, radio del Magnetar, costes y el estado de la carga del
    // Púlsar—. Datos sin comportamiento: EnemyEvaluator no toca nada de Unity salvo Vector2.
    //
    // 12.6: no lleva protones que no se han lanzado, y la carga del Púlsar es lo que enseña su barra,
    // que se ve en los dos bandos (7.5).
    public sealed class BoardNode
    {
        public BoardNode(int index, NodeSnapshot state, Vector2 position, float magnetarRadius = 0f,
                         int upgradeCost = -1, int conversionCost = 0,
                         PowerType? chargingPower = null, PowerType? readyPower = null, bool powerUpMarked = false)
        {
            Index = index;
            State = state;
            Position = position;
            MagnetarRadius = magnetarRadius;
            UpgradeCost = upgradeCost;
            ConversionCost = conversionCost;
            ChargingPower = chargingPower;
            ReadyPower = readyPower;
            PowerUpMarked = powerUpMarked;
        }

        // Orden de NodeRegistry: el desempate de 12.3.3.
        public int Index { get; }
        public NodeSnapshot State { get; }
        public Vector2 Position { get; }

        // Radio de disparo si es Magnetar (4.4); 0 si no.
        public float MagnetarRadius { get; }

        // Coste del nivel siguiente (16.2); -1 al máximo.
        public int UpgradeCost { get; }

        // Coste de reconvertirlo (16.4).
        public int ConversionCost { get; }

        // Púlsar cargando ese poder, o con ese poder listo (7.1).
        public PowerType? ChargingPower { get; }
        public PowerType? ReadyPower { get; }

        // Asteroide neutral con un power-up encima (9); hasta T5-04, nunca.
        public bool PowerUpMarked { get; }

        public Faction Faction => State.Faction;
        public NodeType Type => State.Type;
        public int Energy => State.Energy;
        public bool IsLit => State.Type != NodeType.Asteroid;
    }

    // El tablero de un ciclo de decisión (12.1, paso 1): todos los nodos en el orden de NodeRegistry,
    // el bando que decide y los datos globales que piden las fórmulas. Lo construye EnemyController
    // (T4-04) a partir de NodeRegistry; los tests, a mano.
    public sealed class EnemyBoard
    {
        public EnemyBoard(Faction self, IReadOnlyList<BoardNode> nodes, float magnetarResistance, int ignitionCost,
                          IReadOnlyList<PowerDefinition> powers)
        {
            if (self == Faction.Neutral)
                throw new ArgumentException("Neutral no decide.", nameof(self));
            Self = self;
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            MagnetarResistance = magnetarResistance;
            IgnitionCost = ignitionCost;
            Powers = powers ?? Array.Empty<PowerDefinition>();
        }

        public Faction Self { get; }

        // El rival: el Jugador cuando decide el Enemigo.
        public Faction Opponent => Self == Faction.Enemy ? Faction.Player : Faction.Enemy;

        public IReadOnlyList<BoardNode> Nodes { get; }
        public float MagnetarResistance { get; }
        public int IgnitionCost { get; }

        // Los tres PowerDefinition, para coste y nivel requerido (16.3).
        public IReadOnlyList<PowerDefinition> Powers { get; }

        public PowerDefinition Power(PowerType type)
        {
            foreach (PowerDefinition power in Powers)
            {
                if (power != null && power.Type == type)
                    return power;
            }
            return null;
        }
    }
}
