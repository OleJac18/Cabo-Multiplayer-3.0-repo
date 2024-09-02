using JetBrains.Annotations;
using System;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardController : NetworkBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Card thisCard = new Card(13, CardStack.Stack.NONE);
    [SerializeField] private bool _canHover = false;
    [SerializeField] private bool _isSelectable = false;

    [CanBeNull] public static event Action GraveyardCardClickedEvent;

    [SerializeField] private TextMeshProUGUI numberTextTopLeft;
    [SerializeField] private TextMeshProUGUI numberTextBottomRight;
    [SerializeField] private GameObject cardBackImage;

    private const float TimeForFlip = 0.25f;
    private Outline _outline;
    private Vector3 _originalScale;
    private Vector3 _hoverScale;
    private PlayerManager _playerManager;

    private void Awake()
    {
        _outline = this.GetComponent<Outline>();
        _originalScale = Vector3.one;
        _hoverScale = new Vector3(1.1f, 1.1f, 1f);
    }

    private void Start()
    {
        _playerManager = PlayerManager.instance;
    }

    /// <summary>
    /// Man muss nur die "CardNumber" setzen und die Karte wird automatisch aktualisiert
    /// get: Gibt die Nummer der Karte zurück
    /// Set: Setzt die Nummer der Karte
    /// </summary>
    public int CardNumber
    {
        get { return thisCard.number; }
        set
        {
            if (thisCard.number != value)
            {
                thisCard.number = value;
                UpdateCardNumber();
            }
        }
    }

    /// <summary>
    /// Aktualisiert die Nummer der Karte
    /// </summary>
    private void UpdateCardNumber()
    {
        string cardNumber = thisCard.number.ToString();
        numberTextTopLeft.text = cardNumber;
        numberTextBottomRight.text = cardNumber;
    }

    public void SetCardBackImageVisibility(bool visible)
    {
        cardBackImage.SetActive(visible);
    }

    public bool GetCardBackImageVisibility()
    {
        return cardBackImage.activeSelf;
    }

    public void SetCanHover(bool canHover)
    {
        _canHover = canHover;
    }

    public bool GetCanHover()
    {
        return _canHover;
    }

    public void SetIsSelectable(bool isSelectable)
    {
        _isSelectable = isSelectable;
    }

    public bool GetIsSelectable()
    {
        return _isSelectable;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Wenn nicht gehovert werden darf, return
        if (!_canHover) return;

        // Überprüfen, ob der aktuelle Spieler am Zug ist
        if (!_playerManager.GetIsPlayerTurn()) return;

        // Bewegt die Karte local
        this.transform.localScale = _hoverScale;
        int index = this.transform.GetSiblingIndex();

        // Bewegt die gleiche Karte beim Enemy
        _playerManager.CardHoverClientRpc(_hoverScale, index);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Wenn nicht gehovert werden darf, return
        if (!_canHover) return;

        // Überprüfen, ob der aktuelle Spieler am Zug ist
        if (!_playerManager.GetIsPlayerTurn()) return;

        // Bewegt die Karte local
        this.transform.localScale = _originalScale;
        int index = this.transform.GetSiblingIndex();

        // Bewegt die gleiche Karte beim Enemy
        _playerManager.CardHoverClientRpc(_originalScale, index);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isSelectable) return;

        // Überprüfen, ob der aktuelle Spieler am Zug ist
        if (!_playerManager.GetIsPlayerTurn()) return;

        if (thisCard.correspondingDeck == CardStack.Stack.GRAVEYARD)
        {
            GraveyardCardClickedEvent?.Invoke();
            return;
        }

        // Überprüft, ob generell noch eine Karte umgedreht werden darf oder ob die selektierte Karte bereits umgedreht ist
        if (_playerManager.GetCardsLeftToLookAt() > 0 || _playerManager.GetFlippedCards(this.transform.GetSiblingIndex()))
        {
            // Ist die Karte noch nicht umgedreht, werden die Karten, die noch angeguckt werden dürfen um 1 reduziert
            // und gespeichert, welche Karte umgedreht worden ist
            if (cardBackImage.activeSelf)
            {
                _playerManager.DecreaseCardsLeftToLookAt();
                _playerManager.SetFlippedCards(this.transform.GetSiblingIndex(), true);
            }
            else
            {
                _playerManager.SetFlippedCards(this.transform.GetSiblingIndex(), false);
            }

            FlipCardAnimation(!cardBackImage.activeSelf);
        }
        else
        {
            // Überprüft, ob sich eine noch umgedrehte Karte auf der Hand des Spielers befindet
            bool flippedCardIsThere = false;
            for (int i = 0; i < 4; i++)
            {
                if (_playerManager.GetFlippedCards(i))
                {
                    flippedCardIsThere = true;
                    break;
                }
                else
                {
                    flippedCardIsThere = false;
                }
            }

            // Überprüft, ob sich der Spieler noch eine Karte anguckt aber bereits zwei Karten umgedreht hat.
            // Wenn ja wird ausgegeben, dass er sich keine weitere Karte angucken darf
            // Sobald zwei Karten angeguckt und wieder umgedreht worden sind, können die einzelnen Karten selektiert werden
            if (flippedCardIsThere)
            {
                Debug.Log("Du darfst keine Karte mehr angucken");
            }
            else
            {
                SelectionAnimation();
            }
        }
    }

    /// <summary>
    /// Führt eine Auswahlanimation für die Karte aus. Wenn die Karte bereits ausgewählt ist, wird die Auswahl aufgehoben.
    /// Wenn die Karte nicht ausgewählt ist, wird sie ausgewählt.
    /// </summary>
    private void SelectionAnimation()
    {
        if (_outline == null)
        {
            Debug.Log("Das Object " + name + " hat keine Komponente Outline");
        }
        else
        {
            if (_outline.enabled)
            {
                _outline.enabled = false;
                int index = this.transform.GetSiblingIndex();
                _playerManager.SetClickedCards(false, index);

                _playerManager.SelectCardClientRpc(false, index);
            }
            else
            {
                _outline.enabled = true;
                int index = this.transform.GetSiblingIndex();
                _playerManager.SetClickedCards(true, index);

                _playerManager.SelectCardClientRpc(true, index);
            }
        }
    }

    // Dreht die Karte 180°. Nach 90° wird die Sichtbarkeit der Rückseite invertiert
    private void FlipCardAnimation(bool visible)
    {
        LeanTween.rotateY(this.gameObject, 90.0f, TimeForFlip).setOnComplete(() =>
        {
            this.SetCardBackImageVisibility(visible);
            LeanTween.rotateY(this.gameObject, 0.0f, TimeForFlip);
        });
    }
}