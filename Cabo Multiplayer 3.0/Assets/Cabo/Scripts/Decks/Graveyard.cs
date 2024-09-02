// Ignore Spelling: Rpc

using JetBrains.Annotations;
using System;
using Unity.Netcode;
using UnityEngine;

public class Graveyard : NetworkBehaviour
{
    [CanBeNull] public static event Action<ulong> CardReachedGraveyardEvent;

    [SerializeField] private GameObject spawnNetworkCardPrefab;
    [SerializeField] private GameObject spawnLocalCardPrefab;
    [SerializeField] private Transform spawnCardDeckPos;
    [SerializeField] private Transform spawnPlayerPos;
    [SerializeField] private Transform spawnEnemyPos;

    [SerializeField] private GameObject _topCard;
    [SerializeField] private int _bottomCardNumber;
    [SerializeField] private bool thereIsASecondCard;
    private bool _hasRunOnHost = false;
    private bool _dontChangePlayerTurn = false;
    private GameObject _spawnedCard;
    private const float TimeForFlip = 0.5f;

    private CardController _topCardController;
    private CardController _spawnedCardController;

    private void Start()
    {
        CardDeck.CardDeckClickedEvent += UpdateValueThereIsASecondCard;
        GameManager.GraveyardSpawnFirstCardEvent += SpawnFirstCard;
        CardController.GraveyardCardClickedEvent += CardClicked;
        PlayerManager.OnCardReachedGraveyard += CardReachedGraveyard;
        PlayerManager.ChangeBottomCardFromGraveyard += UpdateBottomCard;
        PlayerManager.OnInvalidCardSelectionForTrade += MoveCardToGraveyardHandler;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        CardDeck.CardDeckClickedEvent -= UpdateValueThereIsASecondCard;
        GameManager.GraveyardSpawnFirstCardEvent -= SpawnFirstCard;
        CardController.GraveyardCardClickedEvent -= CardClicked;
        PlayerManager.OnCardReachedGraveyard -= CardReachedGraveyard;
        PlayerManager.ChangeBottomCardFromGraveyard -= UpdateBottomCard;
        PlayerManager.OnInvalidCardSelectionForTrade -= MoveCardToGraveyardHandler;
    }

    private void SpawnFirstCard()
    {
        Debug.Log("Spawn GraveyardCard");
        _topCard = Instantiate(spawnNetworkCardPrefab);
        _topCardController = _topCard.GetComponent<CardController>();

        var topCardNetworkObject = _topCard.GetComponent<NetworkObject>();
        topCardNetworkObject.Spawn();
        topCardNetworkObject.TrySetParent(this.transform);

        _topCard.transform.localScale = Vector3.one;
        _topCard.transform.position = spawnCardDeckPos.position;
        SetCardBackImageVisibilityClientRpc(true, 0);

        int cardDeckTopCard = CardDeck.instance.DrawTopCard();
        SetCardDefaultSettingsClientRpc(cardDeckTopCard, 0);

        LeanTween.move(_topCard, this.transform.position, 0.5f).setOnComplete(FlipCard);
    }

    // Dreht die Karte um
    private void FlipCard()
    {
        LeanTween.rotateY(_topCard, 90.0f, TimeForFlip).setOnComplete(() =>
        {
            SetCardBackImageVisibilityClientRpc(false, 0);

            LeanTween.rotateY(_topCard, 0.0f, TimeForFlip);
        });
    }

    private void UpdateValueThereIsASecondCard()
    {
        //CardDeck.CardDeckClickedEvent -= UpdateValueThereIsASecondCard;
        _bottomCardNumber = _topCardController.CardNumber;
        thereIsASecondCard = true;
    }

    private void UpdateBottomCard(int newBottomCardNumber)
    {
        _bottomCardNumber = newBottomCardNumber;
        thereIsASecondCard = true;
    }

    private void CardReachedGraveyard(int newTopCardNumber)
    {
        _topCardController.CardNumber = newTopCardNumber;
        _topCard.SetActive(true);
        DestroyAllButTopCard();
    }

    private void DestroyAllButTopCard()
    {
        for (int i = 1; i < this.transform.childCount; i++)
        {
            Destroy(this.transform.GetChild(i).gameObject);
        }
    }

    private bool ShouldReturnOnServerOrHost()
    {
        // Wenn der Code auf dem Host läuft und bereits einmal ausgeführt wurde, brechen wir ab
        if (IsHost && _hasRunOnHost) return true;

        // Wenn der Code auf dem Host läuft und noch nicht ausgeführt wurde, setzen wir hasRunOnHost auf true
        if (IsHost && !_hasRunOnHost) _hasRunOnHost = true;

        return false;
    }

    public void CardClicked()
    {
        // Wenn es einen reinen Server gibt, wird auf dem Server keine Interaktion ausgeführt
        if (IsServer && !IsHost) return;

        Debug.Log(name + " clicked!");

        bool spawnACard = SpawnCard();
        if (spawnACard)
        {
            if (thereIsASecondCard)
            {
                _topCard.SetActive(true);
                _topCardController.CardNumber = _bottomCardNumber;
            }
            else
            {
                _topCard.SetActive(false);
            }
            LeanTween.move(_spawnedCard, spawnPlayerPos.position, 0.5f);
            SpawnGraveyardCardClientRpc();
        }
    }

    public bool SpawnCard()
    {
        //Überprüft ob es bereits eine gespawnte Karte von einem der Kartenstapel gibt
        if (_spawnedCard == null)
        {
            _spawnedCard = GameObject.FindGameObjectWithTag("SpawnDeck");
        }

        if (_topCard == null)
        {
            _topCard = this.transform.GetChild(0).gameObject;
            Debug.Log("Die topCard wurde hinzugefügt");
            _topCardController = _topCard.GetComponent<CardController>();
        }

        // Spawned eine neue Karte, wenn es nicht bereits schon eine gibt. Diese ist die oberste vom Kartenstapel
        // Diese bewegt sich vom Kartenstapel auf eine definierte Position beim Spieler
        // Nachdem sie bei der Position angekommen ist, wird sie umgedreht
        if (_topCard.activeSelf)
        {
            if (_spawnedCard == null)
            {
                _spawnedCard = Instantiate(spawnLocalCardPrefab, this.transform.position, this.transform.rotation);
                _spawnedCard.transform.SetParent(this.transform);
                _spawnedCard.tag = "SpawnDeck";
                _spawnedCardController = _spawnedCard.GetComponent<CardController>();
                _spawnedCardController.thisCard.correspondingDeck = CardStack.Stack.GRAVEYARD;
                _spawnedCard.transform.localScale = Vector3.one;
                _spawnedCardController.SetCardBackImageVisibility(false);
                _spawnedCardController.CardNumber = _topCardController.CardNumber;
                return true;
            }
            else
            {
                Debug.Log("Es wurde bereits eine Karte gezogen");
                return false;
            }
        }
        else
        {
            Debug.Log("Es befindet sich keine Karte auf dem Ablagestapel");
            return false;
        }
    }

    public void MoveCardToGraveyardHandler()
    {
        // Wenn es einen reinen Server gibt, dann wurde bei ihm keine Karte gespawned und somit kann auch keine Karte zurück auf das Graveyard
        // gelegt werden
        if (IsServer && !IsHost) return;

        if(!PlayerManager.instance.GetIsPlayerTurn())
        {
            Debug.Log("Du bist nicht am Zug");
            return;
        }

        if (_spawnedCard == null)
        {
            _spawnedCard = GameObject.FindGameObjectWithTag("SpawnDeck");
            
            if (_spawnedCard == null)
            {
                Debug.Log("Es wurde keine Karte gezogen. Ziehe eine Karte, um Ablegen zu können.");
                return;
            }

            _spawnedCardController = _spawnedCard.GetComponent<CardController>();
        }

        int cardNumber = _spawnedCardController.CardNumber;

        MoveSpawnedCardToGraveyard(spawnPlayerPos.position, cardNumber);
        MoveSpawnedCardToGraveyardClientRpc(cardNumber);
    }

    ///  <summary>
    /// Bewegt entweder die gezogene Karte oder selektierte/n Karte/n auf den Graveyard und zerstört sie danach
    ///  </summary>
    private void MoveSpawnedCardToGraveyard(Vector3 spawnPos, int spawnedCardNumber)
    {
        if (_spawnedCard == null)
        {
            _spawnedCard = GameObject.FindGameObjectWithTag("SpawnDeck");

            if (_spawnedCard == null)
            {
                Debug.Log("Es wurde keine Karte gezogen. Ziehe eine Karte, um Ablegen zu können.");
                return;
            }

            _spawnedCardController = _spawnedCard.GetComponent<CardController>();
        }

        if (_spawnedCardController.thisCard.correspondingDeck == CardStack.Stack.CARDDECK)
        {
            _spawnedCard.transform.SetParent(this.transform);
        }

        Vector3 spawnedCardPos = _spawnedCard.transform.position;

        if (_spawnedCard != null)
        {
            // Floatzahlen sind niemals exakt gleich. Aus diesem Grund benutze ich hier Vector3.Distance
            if (Vector3.Distance(spawnedCardPos, spawnPos) < .001f)
            {
                LeanTween.move(_spawnedCard, this.transform.position, 0.5f).setOnComplete(() =>
                {
                    if (_spawnedCardController.thisCard.correspondingDeck == CardStack.Stack.CARDDECK)
                    {
                        _bottomCardNumber = _topCardController.CardNumber;
                    }

                    _topCardController.CardNumber = spawnedCardNumber;
                    _topCard.SetActive(true);
                    Destroy(_spawnedCard);
                    if (IsHost) _hasRunOnHost = false;
                    if (!_dontChangePlayerTurn)
                    {
                        CardReachedGraveyardEvent?.Invoke(NetworkManager.Singleton.LocalClientId);
                    }
                    _dontChangePlayerTurn = false;
                });
            }
            else
            {
                Debug.Log("Es befindet sich keine Karte auf der spawnPlayerPos");
            }
        }
        else
        {
            Debug.Log("Es kann keine Karte abgelegt werden");
        }
    }

    ////////////////////////////////////////////////////////////////////////////

    /// <summary>
    ///  Ändert die Sichtbarkeit der Backcard
    ///  True: Sie ist zu sehen, False: Sie ist nicht zu sehen, heißt die Karten mit der Kartennummer etc. ist zu sehen
    /// </summary>
    /// <param name="visible"></param>
    /// <param name="childNumber"></param>
    [Rpc(SendTo.Everyone)]
    public void SetCardBackImageVisibilityClientRpc(bool visible, int childNumber)
    {
        GameObject topCard = this.transform.GetChild(childNumber).gameObject;
        topCard.GetComponent<CardController>().SetCardBackImageVisibility(visible);
    }

    /// <summary>
    /// Setzt die Kartennummer bei allen Clients und Server
    /// </summary>
    /// <param name="cardNumber"></param>
    /// <param name="childNumber"></param>
    [Rpc(SendTo.Everyone)]
    public void SetCardDefaultSettingsClientRpc(int cardNumber, int childNumber)
    {
        if (_topCard == null)
        {
            _topCard = this.transform.GetChild(0).gameObject;
            _topCardController = _topCard.GetComponent<CardController>();
        }
        _topCardController.CardNumber = cardNumber;
        _topCardController.thisCard.correspondingDeck = CardStack.Stack.GRAVEYARD;
        _topCardController.SetIsSelectable(true);
    }

    /// <summary>
    /// Spawned eine Graveyard Karte bei allen Clients/Server, die nicht auf die Graveyard Karte geklickt haben
    /// </summary>
    [Rpc(SendTo.NotMe)]
    public void SpawnGraveyardCardClientRpc()
    {
        // Wenn es einen reinen Server gibt, wird auf dem Server keine Interaktion ausgeführt
        if (IsServer && !IsHost) return;

        if (ShouldReturnOnServerOrHost()) return;

        Debug.Log("Client Spawned a Graveyard Card!");

        bool spawnACard = SpawnCard();
        if (spawnACard)
        {
            if (thereIsASecondCard)
            {
                _topCard.SetActive(true);
                _topCardController.CardNumber = _bottomCardNumber;
            }
            else
            {
                _topCard.SetActive(false);
            }

            LeanTween.move(_spawnedCard, spawnEnemyPos.position, 0.5f).setOnComplete(() =>
            {
                _hasRunOnHost = false;
            });
        }
    }

    /// <summary>
    /// Bewegt entweder die gezogene Karte oder selektierte/n Karte/n bei allen Clients auf den Graveyard und zerstört sie danach
    /// </summary>
    [Rpc(SendTo.NotMe)]
    public void MoveSpawnedCardToGraveyardClientRpc(int spawnedCardNumber)
    {
        // Wenn es einen reinen Server gibt, dann wurde bei ihm keine Karte gespawned und somit kann auch keine Karte zurück auf das Graveyard
        // gelegt werden
        if (IsServer && !IsHost) return;

        if (ShouldReturnOnServerOrHost()) return;

        Debug.Log("Client bewegt die Karte/n auf den Graveyard!");
        _dontChangePlayerTurn = true;
        MoveSpawnedCardToGraveyard(spawnEnemyPos.position, spawnedCardNumber);
    }
}