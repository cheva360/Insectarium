using UnityEngine;
using UnityEngine.EventSystems;

public class CursorHoverSwap : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    private static int _hoverCount = 0;

    void OnDisable()
    {
        // If this button is hidden while hovered, clean up
        if (_hoverCount > 0)
        {
            _hoverCount--;
            if (_hoverCount == 0)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hoverCount++;
        if (hoverCursor != null)
            Cursor.SetCursor(hoverCursor, hotspot, CursorMode.Auto);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hoverCount = Mathf.Max(0, _hoverCount - 1);
        if (_hoverCount == 0)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}