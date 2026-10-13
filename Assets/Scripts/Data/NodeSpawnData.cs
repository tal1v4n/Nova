using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // Un nodo de partida de un nivel (15.4, T5-01): dónde está, qué es, de quién y con cuánta
    // Energía. Los valores de los nueve niveles son los del apéndice del maestro (T5-02).
    [Serializable]
    public struct NodeSpawnData
    {
        [Tooltip("Centro del nodo en u, con el origen en el centro del tablero de 15 × 8.4375 u.")]
        [SerializeField] Vector2 position;

        [SerializeField] NodeType type;

        [Tooltip("0 = el de partida del tipo: 0 el Asteroide, 1 el resto.")]
        [SerializeField, Min(0)] int level;

        [SerializeField] Faction faction;

        [Tooltip("Energía inicial (18 #37). Entre 0 y el Tope del tipo: LevelLoader rechaza el nivel si no.")]
        [SerializeField, Min(0)] int initialEnergy;

        public NodeSpawnData(Vector2 position, NodeType type, Faction faction, int initialEnergy, int level = 0)
        {
            this.position = position;
            this.type = type;
            this.level = level;
            this.faction = faction;
            this.initialEnergy = initialEnergy;
        }

        public Vector2 Position => position;
        public NodeType Type => type;
        public int Level => level;
        public Faction Faction => faction;
        public int InitialEnergy => initialEnergy;
    }
}
