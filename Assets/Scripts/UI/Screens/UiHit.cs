using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nova.UI
{
    // Un área de los menús que responde al cursor sin dibujar nada: las opciones del menú principal y
    // los nodos de la carta estelar. Necesita un Graphic para recibir rayos; pone uno transparente.
    [DisallowMultipleComponent]
    internal sealed class UiHit : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public event Action OnEnter;
        public event Action OnExit;
        public event Action OnClick;

        public static UiHit Add(RectTransform target)
        {
            if (target.GetComponent<Graphic>() == null)
                target.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            else
                target.GetComponent<Graphic>().raycastTarget = true;
            return target.gameObject.AddComponent<UiHit>();
        }

        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                OnClick?.Invoke();
        }
    }
}
