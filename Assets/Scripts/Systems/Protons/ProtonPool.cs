using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Systems
{
    // Object pooling de protones (15.6). Los protones libres esperan desactivados como hijos
    // del pool; el lanzamiento los activa y el impacto los devuelve.
    [DisallowMultipleComponent]
    public sealed class ProtonPool : MonoBehaviour
    {
        readonly Stack<Proton> free = new Stack<Proton>();
        int created;

        public int FreeCount => free.Count;
        public int CreatedCount => created;

        public Proton Get()
        {
            Proton proton = free.Count > 0 ? free.Pop() : Create();
            proton.gameObject.SetActive(true);
            return proton;
        }

        public void Release(Proton proton)
        {
            if (proton == null)
                throw new ArgumentNullException(nameof(proton));
            if (proton.Pool != this)
                throw new InvalidOperationException("Este protón no es de este pool.");
            if (proton.InFlight)
                throw new InvalidOperationException("No se devuelve un protón en vuelo.");
            if (free.Contains(proton))
                return;

            proton.gameObject.SetActive(false);
            free.Push(proton);
        }

        Proton Create()
        {
            var go = new GameObject($"Proton {created}");
            go.transform.SetParent(transform, false);
            Proton proton = go.AddComponent<Proton>();
            proton.Pool = this;
            created++;
            return proton;
        }
    }
}
