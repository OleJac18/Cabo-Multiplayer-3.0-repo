using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerManager : NetworkBehaviour
{
    public static PlayerManager instance; // Ist für größere Projekte nicht angemessen

    [CanBeNull] public static event Action<int> OnCardReachedGraveyard;

    [CanBeNull] public static event Action<int> ChangeBottomCardFromGraveyard;

    [CanBeNull] public static event Action OnInvalidCardSelectionForTrade;

    [CanBeNull] public static event Action<ulong> CardReachedPlayerHandEvent;


    public List<ulong> playerIds = new List<ulong>();

    [Header("UI Elements")]
    [SerializeField] private GameObject playerHand;
    [SerializeField] private GameObject enemyHand;
    [SerializeField] private Transform graveyardTransform;

    [SerializeField] private GameObject spawnLocalCardPrefab;
    [SerializeField] private int cardsLeftToLookAt;
    [SerializeField] private bool[] flippedCards;

    [SerializeField] private bool _isPlayerTurn = false;
    [SerializeField] private PlayerStatsController[] _playerStatsControllers = new PlayerStatsController[GameManager.playerCount];

    private bool _hasRunOnHost = false;
    private GameObject _deck;
    [SerializeField] private bool[] _clickedCards;
    [SerializeField] private GameObject[] _selectedCards;
    private int _firstClickedCard;
    private int _lastClickedCard;
    private GameObject _firstSelectedCard;
    private GameObject _spawnedCard;

    private const float TimeForFlip = 0.5f;

    // Start is called before the first frame update
    private void Start()
    {
        instance = this;
        cardsLeftToLookAt = 2;
        flippedCards = new bool[4];
        _clickedCards = new bool[4];

        GameManager.ResetHasRunOnHostEvent += SetHasRunOnHost;
        CardManager.OnSendPlayerIds += SendPlayerIdsClientRpc;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        GameManager.ResetHasRunOnHostEvent -= SetHasRunOnHost;
        CardManager.OnSendPlayerIds -= SendPlayerIdsClientRpc;
    }

    // Gibt das PlayerHandPanel zurück. Ist dafür, dass nicht jedes Script eine Referenzverknüpfung braucht
    public GameObject GetPlayerHandPanel()
    {
        return playerHand;
    }

    // Gibt das EnemyHandPanel zurück. Ist dafür, dass nicht jedes Script eine Referenzverknüpfung braucht
    public GameObject GetEnemyHandPanel()
    {
        return enemyHand;
    }

    public int GetCardsLeftToLookAt()
    {
        return cardsLeftToLookAt;
    }

    public void DecreaseCardsLeftToLookAt()
    {
        cardsLeftToLookAt--;
    }

    public bool GetFlippedCards(int position)
    {
        return flippedCards[position];
    }

    public void SetFlippedCards(int position, bool selected)
    {
        flippedCards[position] = selected;
    }

    public void SetIsPlayerTurn(bool isPlayerTurn)
    {
        _isPlayerTurn = isPlayerTurn;
    }

    public bool GetIsPlayerTurn()
    {
        return _isPlayerTurn;
    }

    public void SetClickedCards(bool IsClicked, int clickedCardIndex)
    {
        _clickedCards[clickedCardIndex] = IsClicked;
    }

    public void SetHasRunOnHost(bool hasExecutedOnHost)
    {
        _hasRunOnHost = hasExecutedOnHost;
    }

    private bool ShouldReturnOnServerOrHost()
    {
        // Wenn der Code auf dem Host läuft und bereits einmal ausgeführt wurde, brechen wir ab
        if (IsHost && _hasRunOnHost) return true;

        // Wenn der Code auf dem Host läuft und noch nicht ausgeführt wurde, setzen wir hasRunOnHost auf true
        if (IsHost && !_hasRunOnHost) _hasRunOnHost = true;

        return false;
    }

    public void ChangeCards()
    {
        if (_isPlayerTurn)
        {
            _deck = playerHand;
        }
        else
        {
            Debug.Log("Du bist nicht am Zug");
            return;
        }

        bool[] statusOfClickedCards = new bool[2];
        statusOfClickedCards = AnalyzeClickedCards(_clickedCards);
        bool equalclickedCard = statusOfClickedCards[0];
        bool thereAreClickedCards = statusOfClickedCards[1];


        // In diesen Schritt wird gesprungen, wenn die angeklickten Karten gleich waren oder nur eine
        if (equalclickedCard)
        {
            int firstClickedCardNumber = _deck.transform.GetChild(_firstClickedCard).gameObject.GetComponent<CardController>().CardNumber;
            MoveCardToGraveyard();
            MoveCardToGraveyardClientRpc(_firstClickedCard, _lastClickedCard, firstClickedCardNumber, _clickedCards);
        }
        else
        {
            if (thereAreClickedCards)
            {
                // Wenn die Karten nicht gleich sind und es gibt angeklickte Karten (also nicht nur eine),
                // wird die gespawnte Karte zum Friedhof bewegt.
                DeactivateCardOutline(_clickedCards);
                DeactivateCardOutlineRpc(_clickedCards);
                OnInvalidCardSelectionForTrade?.Invoke();
            }
            else
                Debug.Log("Es wurde keine Karte gezogen. Ziehe eine Karte, um Tauschen zu können.");
        }

    }

    /// <summary>
    /// Analysiert die angeklickten Karten und gibt den Status der Karten zurück.
    /// </summary>
    /// <param name="clickedCards">Ein Array von booleschen Werten, das die angeklickten Karten repräsentiert.</param>
    /// <returns>Ein Array von booleschen Werten, das den Status der angeklickten Karten enthält.
    /// equalclickedCard gibt an, ob die angeklickten Karten gleich sind.
    /// thereAreClickedCards gibt an, ob es angeklickte Karten gibt.</returns>
    private bool[] AnalyzeClickedCards(bool[] clickedCards)
    {
        _firstClickedCard = -1;
        _lastClickedCard = -1;
        int firstNumber = -1;

        // Interne Variablen
        int drawNumber = -1;
        bool firstPosition = true;
        bool equalclickedCard = false;
        bool thereAreClickedCards = false;

        int childCount = _deck.transform.childCount;

        // Überprüft, ob die angeklickten Karten gleich sind
        // Speichert die Position auf der Hand der ersten angeklickten Karte
        // Speichert die Nummer der ersten angeklickten Karte
        for (int i = 0; i < childCount; i++)
        {
            // Wenn die Karte nicht angeklickt wurde, wird zum nächsten Schritt gesprungen
            if (!clickedCards[i]) continue;

            GameObject selectedCard = _deck.transform.GetChild(i).gameObject;
            drawNumber = selectedCard.GetComponent<CardController>().CardNumber;

            if (firstPosition)
            {
                _firstClickedCard = i;
                firstNumber = drawNumber;
                firstPosition = false;
                equalclickedCard = true;
                thereAreClickedCards = true;
            }
            else if (firstNumber != drawNumber)
            {
                Debug.Log("Die Karten sind ungleich. Du kannst nicht beide Tauschen. Extra 3 PUNKTE und Ende des Zuges");
                equalclickedCard = false;
                break;
            }
            _lastClickedCard = i;
        }

        bool[] statusOfClickedCards = { equalclickedCard, thereAreClickedCards };
        return statusOfClickedCards;
    }

    /// <summary>
    /// Bewegt eine oder mehrere Karten zum Friedhof (Graveyard).
    /// </summary>
    private void MoveCardToGraveyard()
    {
        _selectedCards = GetSelectedCards(_clickedCards);
        DeactivateCardOutline(_clickedCards);

        // Bewegt alle Karten die angeklickt worden sind zum Graveyard
        MoveSelectedCardsToGraveyard();
    }

    private void SetVariablesForChangeCards(int firstClickedCard, int lastClickedCard, int firstClickedCardNumber, bool[] clickedCards)
    {
        _deck = enemyHand;
        _firstClickedCard = firstClickedCard;
        _deck.transform.GetChild(lastClickedCard).gameObject.GetComponent<CardController>().CardNumber = firstClickedCardNumber;
        _lastClickedCard = lastClickedCard;
        _clickedCards = clickedCards;
    }

    /// <summary>
    /// Deaktiviert die Outline aller ausgewählten Karten.
    /// </summary>
    /// <param name="clickedCards">Ein Array von booleschen Werten, das die ausgewählten Karten repräsentiert.</param>
    private void DeactivateCardOutline(bool[] clickedCards)
    {
        for (int i = 0; i < _deck.transform.childCount; i++)
        {
            if (clickedCards[i])
            {
                Outline outline = _deck.transform.GetChild(i).GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;

                    SelectCardClientRpc(false, i);
                }
            }
        }
    }

    private void ResetClickedCards()
    {
        Array.Fill(_clickedCards, false);
    }

    private GameObject[] GetSelectedCards(bool[] clickedCards)
    {
        GameObject[] selectedCards = new GameObject[4];
        for (int i = 0; i < _deck.transform.childCount; i++)
        {
            if (clickedCards[i])
            {
                selectedCards[i] = _deck.transform.GetChild(i).gameObject;
            }
        }
        return selectedCards;
    }

    private void MoveSelectedCardsToGraveyard()
    {
        for (int i = 0; i < _deck.transform.childCount; i++)
        {
            if (_clickedCards[i])
            {
                // Erstellt ein neues GameObject, das die Position der Karte speichert und als Placeholder dient
                GameObject placeholder = Instantiate(spawnLocalCardPrefab, this.transform.position, this.transform.rotation);
                placeholder.transform.SetParent(_deck.transform);
                placeholder.transform.SetSiblingIndex(i);
                placeholder.transform.localScale = Vector3.one;
                placeholder.GetComponent<CardController>().SetCanHover(true);
                placeholder.GetComponent<CardController>().SetIsSelectable(true);
                // Eine CanasGroup kann die Eigenschaften für alle Kinderobjekte steuern. In diesem Fall wird bei allen
                // Kindern die Transparenz auf 0 gesetzt, damit die Karte nicht mehr sichtbar ist
                placeholder.GetComponent<CanvasGroup>().alpha = 0;

                // Ändert den Parent der Karte, damit sie sich nicht beim Bewegen zum Friedhof unter den Friedofkarten befindet
                _selectedCards[i].transform.SetParent(graveyardTransform);
                _selectedCards[i].GetComponent<CardController>().thisCard.correspondingDeck = CardStack.Stack.GRAVEYARD;

                if (i == _lastClickedCard)
                {
                    LeanTween.move(_selectedCards[_lastClickedCard], graveyardTransform.position, TimeForFlip).setOnComplete(() =>
                    {
                        // Dreht die Karte um
                        LeanTween.rotateY(_selectedCards[_lastClickedCard], 90.0f, TimeForFlip).setOnComplete(() =>
                        {
                            _selectedCards[_lastClickedCard].GetComponent<CardController>().SetCardBackImageVisibility(false);
                            LeanTween.rotateY(_selectedCards[_lastClickedCard], 0.0f, TimeForFlip).setOnComplete(() =>
                            {
                                MoveSpawnedCardToPlayerHand();
                            });
                        });
                    });
                }
                else
                {
                    LeanTween.move(_selectedCards[i], graveyardTransform.position, TimeForFlip);
                }
            }
        }
    }

    ///  <summary>
    /// Führt die verschiedenen Actionen für einen Tausch von Karten aus
    /// Je nachdem, ob eine Karte zum Tauschen angeklickt worden ist, sind diese gleich oder ungleich
    ///  </summary>
    private void MoveSpawnedCardToPlayerHand()
    {
        // // Ändert die Nummer einer Karte vom Spieler auf die vom Kartenstapel/Ablagestapel
        _firstSelectedCard = _deck.transform.GetChild(_firstClickedCard).gameObject;
        _spawnedCard = GameObject.FindGameObjectWithTag("SpawnDeck");
        if (_spawnedCard != null)
        {
            Debug.Log("Es gibt eine gespawnte Karte");
            int rotation;
            if (_isPlayerTurn) { rotation = 1; }
            else { rotation = -1; }

            Vector3[] points = this.GetComponent<MoveInCircle>().CalculateCircle(8, _spawnedCard.transform, _firstSelectedCard.transform, rotation, 100);
            LeanTween.moveSpline(_spawnedCard, points, TimeForFlip).setOnComplete(() =>
            {
                if (_isPlayerTurn)
                {
                    FlipCard(_spawnedCard);
                }
                else
                {
                    UpdateFirstSelectedCardForNotActivePlayer();
                    DestroyAllButFirstCard();

                    int topCardNumber = _selectedCards[_lastClickedCard].GetComponent<CardController>().CardNumber;
                    int bottomCardNumber = _selectedCards[_lastClickedCard].GetComponent<CardController>().CardNumber;

                    OnCardReachedGraveyard?.Invoke(topCardNumber);
                    if (_firstClickedCard != _lastClickedCard)
                    {
                        ChangeBottomCardFromGraveyard?.Invoke(bottomCardNumber);
                    }
                    ResetClickedCards();
                }
            });
        }
        else
        {
            Debug.Log("Es gibt keine gespawnte Karte");
        }
    }

    // Dreht die Karte um
    private void FlipCard(GameObject card)
    {
        LeanTween.rotateY(card, 90.0f, TimeForFlip).setOnComplete(() =>
        {
            card.GetComponent<CardController>().SetCardBackImageVisibility(true);

            LeanTween.rotateY(card, 0.0f, TimeForFlip).setOnComplete(() =>
            {
                UpdateFirstSelectedCardForActivePlayer();
                DestroyAllButFirstCard();

                int topCardNumber = _selectedCards[_lastClickedCard].GetComponent<CardController>().CardNumber;
                int bottomCardNumber = _selectedCards[_lastClickedCard].GetComponent<CardController>().CardNumber;

                OnCardReachedGraveyard?.Invoke(topCardNumber);
                if (_firstClickedCard != _lastClickedCard)
                {
                    ChangeBottomCardFromGraveyard?.Invoke(bottomCardNumber);
                }
                CardReachedPlayerHandEvent?.Invoke(NetworkManager.Singleton.LocalClientId);
                ResetClickedCards();
            });
        });
    }

    /// <summary>
    /// Aktualisiert die Kartennummer der ersten ausgewählten Karte (_firstSelectedCard) auf die Kartennummer
    /// der gespawnten Karte (_spawnedCard) und zerstört die gespawnte Karte.
    /// </summary>
    private void UpdateFirstSelectedCardForActivePlayer()
    {
        // Überprüfen Sie, ob _firstSelectedCard und _spawnedCard nicht null sind
        if (_firstSelectedCard != null && _spawnedCard != null)
        {
            // Aktualisieren Sie die Kartennummer von _firstSelectedCard
            CardController firstCardCardController = _firstSelectedCard.GetComponent<CardController>();
            firstCardCardController.CardNumber = _spawnedCard.GetComponent<CardController>().CardNumber;
            firstCardCardController.SetCanHover(true);
            firstCardCardController.SetIsSelectable(true);
            firstCardCardController.thisCard.correspondingDeck = CardStack.Stack.PLAYERCARD;
            _firstSelectedCard.GetComponent<CanvasGroup>().alpha = 1;

            // Zerstören Sie _spawnedCard
            Destroy(_spawnedCard);
        }
    }

    /// <summary>
    /// Aktualisiert die Kartennummer der ersten ausgewählten Karte (_firstSelectedCard) auf die Kartennummer
    /// der gespawnten Karte (_spawnedCard) und zerstört die gespawnte Karte.
    /// </summary>
    private void UpdateFirstSelectedCardForNotActivePlayer()
    {
        // Überprüfen Sie, ob _firstSelectedCard und _spawnedCard nicht null sind
        if (_firstSelectedCard != null && _spawnedCard != null)
        {
            // Aktualisieren Sie die Kartennummer von _firstSelectedCard
            CardController firstCardCardController = _firstSelectedCard.GetComponent<CardController>();
            firstCardCardController.CardNumber = 13;
            firstCardCardController.SetCanHover(false);
            firstCardCardController.SetIsSelectable(false);
            firstCardCardController.thisCard.correspondingDeck = CardStack.Stack.ENEMYCARD;
            _firstSelectedCard.GetComponent<CanvasGroup>().alpha = 1;

            // Zerstören Sie _spawnedCard
            Destroy(_spawnedCard);
        }
    }

    /// <summary>
    /// Durchläuft alle Kinder von _deck und zerstört alle Karten, die angeklickt wurden und nicht die erste ausgewählte Karte sind.
    /// </summary>
    private void DestroyAllButFirstCard()
    {
        // Durchlaufen Sie alle Kinder von _deck
        for (int i = 0; i < _deck.transform.childCount; i++)
        {
            // Wenn die aktuelle Karte nicht _firstSelectedCard ist und angeklickt wurde, wird sie zerstört
            GameObject currentCard = _deck.transform.GetChild(i).gameObject;
            if (currentCard != _firstSelectedCard && _clickedCards[i])
            {
                Destroy(currentCard);
            }
        }
    }


    /////////////////////////////////////////////////////////////////////////////

    /// <summary>
    ///
    /// </summary>
    /// <param name="scaleBy"></param>
    /// <param name="index"></param>
    [Rpc(SendTo.NotMe)]
    public void CardHoverClientRpc(Vector3 scaleby, int index)
    {
        if (IsServer && !IsHost) return;
        GameObject card = enemyHand.transform.GetChild(index).gameObject;
        card.transform.localScale = scaleby;
    }

    [Rpc(SendTo.NotMe)]
    public void SelectCardClientRpc(bool isSelected, int index)
    {
        if (IsServer && !IsHost) return;

        GameObject card = enemyHand.transform.GetChild(index).gameObject;
        card.GetComponent<Outline>().enabled = isSelected;
    }

    [Rpc(SendTo.NotMe)]
    public void DeactivateCardOutlineRpc(bool[] clickedCards)
    {
        if (IsServer && !IsHost) return;

        DeactivateCardOutline(clickedCards);
    }

    [Rpc(SendTo.NotMe)]
    private void MoveCardToGraveyardClientRpc(int firstClickedCard, int lastClickedCard, int firstClickedCardNumber, bool[] clickedCards)
    {
        if (ShouldReturnOnServerOrHost()) return;

        SetVariablesForChangeCards(firstClickedCard, lastClickedCard, firstClickedCardNumber, clickedCards);
        MoveCardToGraveyard();
    }

    /// <summary>
    /// In dieser Methode werden die Spieler-IDs für die PlayerStatsController gesetzt
    /// </summary>
    /// <param name="playerIdsString">Liste der playerIds als String</param>
    /// <param name="rpcParams"></param>
    [Rpc(SendTo.SpecifiedInParams)]
    private void SendPlayerIdsClientRpc(string playerIdsString, RpcParams rpcParams = default)
    {
        // Konvertieren Sie den String zurück in eine Liste
        playerIds = new List<ulong>(Array.ConvertAll(playerIdsString.Split(','), ulong.Parse));

        int currentPlayerIndex = 0;


        for (int i = 0; i < GameManager.playerCount; i++)
        {
            int currentPlayer = i + 1;
            // Finden Sie den PlayerStatsController mit dem gegebenen Tag
            _playerStatsControllers[i] = GameObject.FindGameObjectWithTag("Player" + currentPlayer).GetComponent<PlayerStatsController>();

            // Der PlayerStatsController mit dem Index 0 ist der lokale Spieler
            if (i == 0)
            {
                _playerStatsControllers[0].SetPlayerId(NetworkManager.Singleton.LocalClientId);
            }
            // Alle weiteren PlayerStatsController sind die weiteren Spieler. Diesen werden die spezifischen Spieler-IDs zugewiesen
            else
            {
                if (playerIds[currentPlayerIndex] == NetworkManager.Singleton.LocalClientId)
                {
                    currentPlayerIndex++;
                }
                _playerStatsControllers[i].SetPlayerId(playerIds[currentPlayerIndex]);
                currentPlayerIndex++;
            }
        }
    }
}