using UnityEngine;
using UnityEngine.EventSystems;

public class PauseMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Tooltip("The '►' or select indicator to the left of this button's label.")]
    [SerializeField] private GameObject selectPrompt;

    [Tooltip("If true, clicking this button calls PauseManager.Unpause().")]
    [SerializeField] private bool isResumeButton = false;

    void OnEnable()
    {
        if (selectPrompt != null)
            selectPrompt.SetActive(false);
    }

    void OnDisable()
    {
        if (selectPrompt != null)
            selectPrompt.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectPrompt != null)
            selectPrompt.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (selectPrompt != null)
            selectPrompt.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isResumeButton && PauseManager.Instance != null)
            PauseManager.Instance.Unpause();
    }
}