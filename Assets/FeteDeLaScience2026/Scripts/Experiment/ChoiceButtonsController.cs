using System;
using UnityEngine;

// Shows/hides the "Quel gateau avez-vous mange ?" panel and turns a poke on one of the
// three flavor buttons into a single Chosen event.
public class ChoiceButtonsController : MonoBehaviour
{
    [Serializable]
    private struct FlavorButton
    {
        public EFlavor Flavor;
        public HandPokeButton Button;
    }

    [SerializeField] private GameObject _root;
    [SerializeField] private FlavorButton[] _buttons;

    public event Action<EFlavor> Chosen;

    private void Awake()
    {
        foreach (FlavorButton entry in _buttons)
        {
            EFlavor flavor = entry.Flavor;
            entry.Button.OnPoked.AddListener(() => HandlePressed(flavor));
        }

        Hide();
    }

    private void HandlePressed(EFlavor flavor)
    {
        Hide();
        Chosen?.Invoke(flavor);
    }

    public void Show()
    {
        if (_root != null) _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }
}
